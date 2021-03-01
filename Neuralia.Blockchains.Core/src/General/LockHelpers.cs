using System;
using System.Collections.Concurrent;
using System.Threading;
using Neuralia.Blockchains.Core.Logging;

namespace Neuralia.Blockchains.Core.General
{
	public class WithLockHelpers
	{
		protected readonly TimeSpan mutexTimeout;

		public WithLockHelpers(TimeSpan mutexTimeout)
		{
			this.mutexTimeout = mutexTimeout;
		}
		
		public T LockRead<T>(object lockObject, Func<ReaderWriterLock, T> f)
		{
			return LockHelpers.LockRead(lockObject, f, this.mutexTimeout);
		}
		
		public T LockWrite<T>(object lockObject, Func<ReaderWriterLock, T> f)
		{
			return LockHelpers.LockWrite(lockObject, f, this.mutexTimeout);
		}
		
		public void LockRead(object lockObject, Action<ReaderWriterLock> f)
		{
			LockHelpers.LockRead(lockObject, f, this.mutexTimeout);
		}
		
		public void LockWrite(object lockObject, Action<ReaderWriterLock> f)
		{
			LockHelpers.LockWrite(lockObject, f, this.mutexTimeout);
		}
	}

	public interface IHasReaderWriterLock
	{
		ReaderWriterLock Mutex { get; }
	}
    public static class LockHelpers
    {
        private static readonly ConcurrentDictionary<object, ReaderWriterLock> mutexes = new ();
    
        	public static ReaderWriterLock GocMutex(object obj)
        	{
        		if (obj == null)
        		{
        			string message = $"Lock object null, this is unexpected...";
        			NLog.LoggingBatcher.Error(message);
        			NLog.LoggingBatcher.Verbose($"Here is the stack trace {Environment.StackTrace}");
        			throw new ArgumentNullException(message);
        		}

                if (obj is IHasReaderWriterLock @l)
	                return @l.Mutex;
                
        		if (mutexes.TryGetValue(obj, out var mutex))
        		{
        			return mutex;
        		}
    
        		return mutexes[obj] = new ReaderWriterLock();
        	}



            public static T LockRead<T>(object lockObject, Func<ReaderWriterLock, T> f, TimeSpan timeout)
            {
	            var mutex = GocMutex(lockObject);

	            try
	            {
		            mutex.AcquireReaderLock(timeout);
		            return f(mutex);
	            }
	            catch (ApplicationException e)
	            {
		            NLog.LoggingBatcher.Error(e, "Failed acquiring mutex lock (read/wrte)");
		            throw;
	            }
	            catch (Exception e)
	            {
		            NLog.LoggingBatcher.Error(e, "Failed during function execution");
		            throw;
	            }
	            finally
	            {
		            mutex.ReleaseLock();
	            }
            }

            public static T LockWrite<T>(object lockObject, Func<ReaderWriterLock, T> f, TimeSpan timeout)
        	{
        		var mutex = GocMutex(lockObject);
        		try
        		{
        			mutex.AcquireWriterLock(timeout);
        			return f(mutex);
        		}
        		catch (ApplicationException e)
        		{
        			NLog.LoggingBatcher.Error(e, "Failed acquiring mutex lock (read)");
        			throw;
        		}
        		catch (Exception e)
        		{
        			NLog.LoggingBatcher.Error(e, "Failed during function execution");
        			throw;
        		}
        		finally
        		{
        			mutex.ReleaseLock();
        		}
        	}
            
            public static void LockRead(object lockObject, Action<ReaderWriterLock> f, TimeSpan timeout)
            {
	            var mutex = GocMutex(lockObject);

	            try
	            {
		            mutex.AcquireReaderLock(timeout);
		            f(mutex);
	            }
	            catch (ApplicationException e)
	            {
		            NLog.LoggingBatcher.Error(e, "Failed acquiring mutex lock (read/wrte)");
		            throw;
	            }
	            catch (Exception e)
	            {
		            NLog.LoggingBatcher.Error(e, "Failed during function execution");
		            throw;
	            }
	            finally
	            {
		            mutex.ReleaseLock();
	            }
            }

            public static void LockWrite(object lockObject, Action<ReaderWriterLock> f, TimeSpan timeout)
            {
	            var mutex = GocMutex(lockObject);
	            try
	            {
		            mutex.AcquireWriterLock(timeout);
		            f(mutex);
	            }
	            catch (ApplicationException e)
	            {
		            NLog.LoggingBatcher.Error(e, "Failed acquiring mutex lock (read)");
		            throw;
	            }
	            catch (Exception e)
	            {
		            NLog.LoggingBatcher.Error(e, "Failed during function execution");
		            throw;
	            }
	            finally
	            {
		            mutex.ReleaseLock();
	            }
            }

    }
}