using System.Collections.Concurrent;

namespace Neuralia.Blockchains.Core.Collections
{
    public class FixedConcurrentQueue<T> : ConcurrentQueue<T> {

        public FixedConcurrentQueue(uint limit) {
            this.Limit = limit;
        }

        public uint Limit { get; set; }

        public new void Enqueue(T obj) {
            this.Enqueue(obj, out T overflow);
        }

        public bool Enqueue(T obj, out T overflow) {
            base.Enqueue(obj);
            overflow = default;

            if(this.Count > this.Limit) {
                this.TryDequeue(out overflow);
                return false;
            }

            return true;
        }
    }
}