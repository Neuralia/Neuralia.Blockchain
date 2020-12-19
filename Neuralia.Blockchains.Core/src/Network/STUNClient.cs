// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Net;
// using System.Threading.Tasks;
// using Neuralia.STUN;
//
// namespace Neuralia.Blockchains.Core.Network {
// 	public class STUNClient {
//
// 		private static readonly List<(string server, int port)> servers = new List<(string server, int port)>();
//
// 		static STUNClient() {
//
// 		#region servers
//
// 			servers.Add(("stun1.l.google.com", 19302));
// 			servers.Add(("stun1.voiceeclipse.net", 3478));
// 			servers.Add(("stun2.l.google.com", 19302));
// 			servers.Add(("stun3.l.google.com", 19302));
// 			servers.Add(("stun4.l.google.com", 19302));
// 			servers.Add(("stunserver.org", 3478));
// 			servers.Add(("stun.l.google.com", 19302));
// 			servers.Add(("stun1.l.google.com", 19302));
// 			servers.Add(("stun2.l.google.com", 19302));
// 			servers.Add(("stun3.l.google.com", 19302));
// 			servers.Add(("stun4.l.google.com", 19302));
// 			servers.Add(("iphone-stun.strato-iphone.de", 3478));
// 			servers.Add(("numb.viagenie.ca", 3478));
// 			servers.Add(("s1.taraba.net", 3478));
// 			servers.Add(("s2.taraba.net", 3478));
// 			servers.Add(("stun.12connect.com", 3478));
// 			servers.Add(("stun.12voip.com", 3478));
// 			servers.Add(("stun.1und1.de", 3478));
// 			servers.Add(("stun.2talk.co.nz", 3478));
// 			servers.Add(("stun.2talk.com", 3478));
// 			servers.Add(("stun.3clogic.com", 3478));
// 			servers.Add(("stun.3cx.com", 3478));
// 			servers.Add(("stun.a-mm.tv", 3478));
// 			servers.Add(("stun.aa.net.uk", 3478));
// 			servers.Add(("stun.acrobits.cz", 3478));
// 			servers.Add(("stun.actionvoip.com", 3478));
// 			servers.Add(("stun.advfn.com", 3478));
// 			servers.Add(("stun.aeta-audio.com", 3478));
// 			servers.Add(("stun.aeta.com", 3478));
// 			servers.Add(("stun.alltel.com.au", 3478));
// 			servers.Add(("stun.altar.com.pl", 3478));
// 			servers.Add(("stun.annatel.net", 3478));
// 			servers.Add(("stun.antisip.com", 3478));
// 			servers.Add(("stun.arbuz.ru", 3478));
// 			servers.Add(("stun.avigora.com", 3478));
// 			servers.Add(("stun.avigora.fr", 3478));
// 			servers.Add(("stun.awa-shima.com", 3478));
// 			servers.Add(("stun.awt.be", 3478));
// 			servers.Add(("stun.b2b2c.ca", 3478));
// 			servers.Add(("stun.bahnhof.net", 3478));
// 			servers.Add(("stun.barracuda.com", 3478));
// 			servers.Add(("stun.bluesip.net", 3478));
// 			servers.Add(("stun.bmwgs.cz", 3478));
// 			servers.Add(("stun.botonakis.com", 3478));
// 			servers.Add(("stun.budgetphone.nl", 3478));
// 			servers.Add(("stun.budgetsip.com", 3478));
// 			servers.Add(("stun.cablenet-as.net", 3478));
// 			servers.Add(("stun.callromania.ro", 3478));
// 			servers.Add(("stun.callwithus.com", 3478));
// 			servers.Add(("stun.cbsys.net", 3478));
// 			servers.Add(("stun.chathelp.ru", 3478));
// 			servers.Add(("stun.cheapvoip.com", 3478));
// 			servers.Add(("stun.ciktel.com", 3478));
// 			servers.Add(("stun.cloopen.com", 3478));
// 			servers.Add(("stun.colouredlines.com.au", 3478));
// 			servers.Add(("stun.comfi.com", 3478));
// 			servers.Add(("stun.commpeak.com", 3478));
// 			servers.Add(("stun.comtube.com", 3478));
// 			servers.Add(("stun.comtube.ru", 3478));
// 			servers.Add(("stun.cope.es", 3478));
// 			servers.Add(("stun.counterpath.com", 3478));
// 			servers.Add(("stun.counterpath.net", 3478));
// 			servers.Add(("stun.cryptonit.net", 3478));
// 			servers.Add(("stun.darioflaccovio.it", 3478));
// 			servers.Add(("stun.datamanagement.it", 3478));
// 			servers.Add(("stun.dcalling.de", 3478));
// 			servers.Add(("stun.decanet.fr", 3478));
// 			servers.Add(("stun.demos.ru", 3478));
// 			servers.Add(("stun.develz.org", 3478));
// 			servers.Add(("stun.dingaling.ca", 3478));
// 			servers.Add(("stun.doublerobotics.com", 3478));
// 			servers.Add(("stun.drogon.net", 3478));
// 			servers.Add(("stun.duocom.es", 3478));
// 			servers.Add(("stun.dus.net", 3478));
// 			servers.Add(("stun.e-fon.ch", 3478));
// 			servers.Add(("stun.easybell.de", 3478));
// 			servers.Add(("stun.easycall.pl", 3478));
// 			servers.Add(("stun.easyvoip.com", 3478));
// 			servers.Add(("stun.efficace-factory.com", 3478));
// 			servers.Add(("stun.einsundeins.com", 3478));
// 			servers.Add(("stun.einsundeins.de", 3478));
// 			servers.Add(("stun.ekiga.net", 3478));
// 			servers.Add(("stun.epygi.com", 3478));
// 			servers.Add(("stun.etoilediese.fr", 3478));
// 			servers.Add(("stun.eyeball.com", 3478));
// 			servers.Add(("stun.faktortel.com.au", 3478));
// 			servers.Add(("stun.freecall.com", 3478));
// 			servers.Add(("stun.freeswitch.org", 3478));
// 			servers.Add(("stun.freevoipdeal.com", 3478));
// 			servers.Add(("stun.fuzemeeting.com", 3478));
// 			servers.Add(("stun.gmx.de", 3478));
// 			servers.Add(("stun.gmx.net", 3478));
// 			servers.Add(("stun.gradwell.com", 3478));
// 			servers.Add(("stun.halonet.pl", 3478));
// 			servers.Add(("stun.hellonanu.com", 3478));
// 			servers.Add(("stun.hoiio.com", 3478));
// 			servers.Add(("stun.hosteurope.de", 3478));
// 			servers.Add(("stun.ideasip.com", 3478));
// 			servers.Add(("stun.imesh.com", 3478));
// 			servers.Add(("stun.infra.net", 3478));
// 			servers.Add(("stun.internetcalls.com", 3478));
// 			servers.Add(("stun.intervoip.com", 3478));
// 			servers.Add(("stun.ipcomms.net", 3478));
// 			servers.Add(("stun.ipfire.org", 3478));
// 			servers.Add(("stun.ippi.fr", 3478));
// 			servers.Add(("stun.ipshka.com", 3478));
// 			servers.Add(("stun.iptel.org", 3478));
// 			servers.Add(("stun.irian.at", 3478));
// 			servers.Add(("stun.it1.hr", 3478));
// 			servers.Add(("stun.ivao.aero", 3478));
// 			servers.Add(("stun.jappix.com", 3478));
// 			servers.Add(("stun.jumblo.com", 3478));
// 			servers.Add(("stun.justvoip.com", 3478));
// 			servers.Add(("stun.kanet.ru", 3478));
// 			servers.Add(("stun.kiwilink.co.nz", 3478));
// 			servers.Add(("stun.kundenserver.de", 3478));
// 			servers.Add(("stun.l.google.com", 19302));
// 			servers.Add(("stun.linea7.net", 3478));
// 			servers.Add(("stun.linphone.org", 3478));
// 			servers.Add(("stun.liveo.fr", 3478));
// 			servers.Add(("stun.lowratevoip.com", 3478));
// 			servers.Add(("stun.lugosoft.com", 3478));
// 			servers.Add(("stun.lundimatin.fr", 3478));
// 			servers.Add(("stun.magnet.ie", 3478));
// 			servers.Add(("stun.manle.com", 3478));
// 			servers.Add(("stun.mgn.ru", 3478));
// 			servers.Add(("stun.mit.de", 3478));
// 			servers.Add(("stun.mitake.com.tw", 3478));
// 			servers.Add(("stun.miwifi.com", 3478));
// 			servers.Add(("stun.modulus.gr", 3478));
// 			servers.Add(("stun.mozcom.com", 3478));
// 			servers.Add(("stun.myvoiptraffic.com", 3478));
// 			servers.Add(("stun.mywatson.it", 3478));
// 			servers.Add(("stun.nas.net", 3478));
// 			servers.Add(("stun.neotel.co.za", 3478));
// 			servers.Add(("stun.netappel.com", 3478));
// 			servers.Add(("stun.netappel.fr", 3478));
// 			servers.Add(("stun.netgsm.com.tr", 3478));
// 			servers.Add(("stun.nfon.net", 3478));
// 			servers.Add(("stun.noblogs.org", 3478));
// 			servers.Add(("stun.noc.ams-ix.net", 3478));
// 			servers.Add(("stun.node4.co.uk", 3478));
// 			servers.Add(("stun.nonoh.net", 3478));
// 			servers.Add(("stun.nottingham.ac.uk", 3478));
// 			servers.Add(("stun.nova.is", 3478));
// 			servers.Add(("stun.nventure.com", 3478));
// 			servers.Add(("stun.on.net.mk", 3478));
// 			servers.Add(("stun.ooma.com", 3478));
// 			servers.Add(("stun.ooonet.ru", 3478));
// 			servers.Add(("stun.oriontelekom.rs", 3478));
// 			servers.Add(("stun.outland-net.de", 3478));
// 			servers.Add(("stun.ozekiphone.com", 3478));
// 			servers.Add(("stun.patlive.com", 3478));
// 			servers.Add(("stun.personal-voip.de", 3478));
// 			servers.Add(("stun.petcube.com", 3478));
// 			servers.Add(("stun.phone.com", 3478));
// 			servers.Add(("stun.phoneserve.com", 3478));
// 			servers.Add(("stun.pjsip.org", 3478));
// 			servers.Add(("stun.poivy.com", 3478));
// 			servers.Add(("stun.thserpbx.org", 3478));
// 			servers.Add(("stun.thservoip.com", 3478));
// 			servers.Add(("stun.ppdi.com", 3478));
// 			servers.Add(("stun.prizee.com", 3478));
// 			servers.Add(("stun.qq.com", 3478));
// 			servers.Add(("stun.qvod.com", 3478));
// 			servers.Add(("stun.rackco.com", 3478));
// 			servers.Add(("stun.rapidnet.de", 3478));
// 			servers.Add(("stun.rb-net.com", 3478));
// 			servers.Add(("stun.refint.net", 3478));
// 			servers.Add(("stun.remote-learner.net", 3478));
// 			servers.Add(("stun.rixtelecom.se", 3478));
// 			servers.Add(("stun.rockenstein.de", 3478));
// 			servers.Add(("stun.rolmail.net", 3478));
// 			servers.Add(("stun.rounds.com", 3478));
// 			servers.Add(("stun.rynga.com", 3478));
// 			servers.Add(("stun.samsungsmartcam.com", 3478));
// 			servers.Add(("stun.schlund.de", 3478));
// 			servers.Add(("stun.services.mozilla.com", 3478));
// 			servers.Add(("stun.sigmavoip.com", 3478));
// 			servers.Add(("stun.sip.us", 3478));
// 			servers.Add(("stun.sipdiscount.com", 3478));
// 			servers.Add(("stun.sipgate.net", 10000));
// 			servers.Add(("stun.sipgate.net", 3478));
// 			servers.Add(("stun.siplogin.de", 3478));
// 			servers.Add(("stun.sipnet.net", 3478));
// 			servers.Add(("stun.sipnet.ru", 3478));
// 			servers.Add(("stun.siportal.it", 3478));
// 			servers.Add(("stun.sippeer.dk", 3478));
// 			servers.Add(("stun.siptraffic.com", 3478));
// 			servers.Add(("stun.skylink.ru", 3478));
// 			servers.Add(("stun.sma.de", 3478));
// 			servers.Add(("stun.smartvoip.com", 3478));
// 			servers.Add(("stun.smsdiscount.com", 3478));
// 			servers.Add(("stun.snafu.de", 3478));
// 			servers.Add(("stun.softjoys.com", 3478));
// 			servers.Add(("stun.solcon.nl", 3478));
// 			servers.Add(("stun.solnet.ch", 3478));
// 			servers.Add(("stun.sonetel.com", 3478));
// 			servers.Add(("stun.sonetel.net", 3478));
// 			servers.Add(("stun.sovtest.ru", 3478));
// 			servers.Add(("stun.speedy.com.ar", 3478));
// 			servers.Add(("stun.spokn.com", 3478));
// 			servers.Add(("stun.srce.hr", 3478));
// 			servers.Add(("stun.ssl7.net", 3478));
// 			servers.Add(("stun.stunprotocol.org", 3478));
// 			servers.Add(("stun.symform.com", 3478));
// 			servers.Add(("stun.symplicity.com", 3478));
// 			servers.Add(("stun.sysadminman.net", 3478));
// 			servers.Add(("stun.t-online.de", 3478));
// 			servers.Add(("stun.tagan.ru", 3478));
// 			servers.Add(("stun.tatneft.ru", 3478));
// 			servers.Add(("stun.teachercreated.com", 3478));
// 			servers.Add(("stun.tel.lu", 3478));
// 			servers.Add(("stun.telbo.com", 3478));
// 			servers.Add(("stun.telefacil.com", 3478));
// 			servers.Add(("stun.tis-dialog.ru", 3478));
// 			servers.Add(("stun.tng.de", 3478));
// 			servers.Add(("stun.twt.it", 3478));
// 			servers.Add(("stun.u-blox.com", 3478));
// 			servers.Add(("stun.ucallweconn.net", 3478));
// 			servers.Add(("stun.ucsb.edu", 3478));
// 			servers.Add(("stun.ucw.cz", 3478));
// 			servers.Add(("stun.uls.co.za", 3478));
// 			servers.Add(("stun.unseen.is", 3478));
// 			servers.Add(("stun.usfamily.net", 3478));
// 			servers.Add(("stun.veoh.com", 3478));
// 			servers.Add(("stun.vidyo.com", 3478));
// 			servers.Add(("stun.vipgroup.net", 3478));
// 			servers.Add(("stun.virtual-call.com", 3478));
// 			servers.Add(("stun.viva.gr", 3478));
// 			servers.Add(("stun.vivox.com", 3478));
// 			servers.Add(("stun.vline.com", 3478));
// 			servers.Add(("stun.vo.lu", 3478));
// 			servers.Add(("stun.vodafone.ro", 3478));
// 			servers.Add(("stun.voicetrading.com", 3478));
// 			servers.Add(("stun.voip.aebc.com", 3478));
// 			servers.Add(("stun.voip.blackberry.com", 3478));
// 			servers.Add(("stun.voip.eutelia.it", 3478));
// 			servers.Add(("stun.voiparound.com", 3478));
// 			servers.Add(("stun.voipblast.com", 3478));
// 			servers.Add(("stun.voipbuster.com", 3478));
// 			servers.Add(("stun.voipbusterpro.com", 3478));
// 			servers.Add(("stun.voipcheap.co.uk", 3478));
// 			servers.Add(("stun.voipcheap.com", 3478));
// 			servers.Add(("stun.voipfibre.com", 3478));
// 			servers.Add(("stun.voipgain.com", 3478));
// 			servers.Add(("stun.voipgate.com", 3478));
// 			servers.Add(("stun.voipinfocenter.com", 3478));
// 			servers.Add(("stun.voipplanet.nl", 3478));
// 			servers.Add(("stun.voippro.com", 3478));
// 			servers.Add(("stun.voipraider.com", 3478));
// 			servers.Add(("stun.voipstunt.com", 3478));
// 			servers.Add(("stun.voipwise.com", 3478));
// 			servers.Add(("stun.voipzoom.com", 3478));
// 			servers.Add(("stun.vopium.com", 3478));
// 			servers.Add(("stun.voxgratia.org", 3478));
// 			servers.Add(("stun.voxox.com", 3478));
// 			servers.Add(("stun.voys.nl", 3478));
// 			servers.Add(("stun.voztele.com", 3478));
// 			servers.Add(("stun.vyke.com", 3478));
// 			servers.Add(("stun.webcalldirect.com", 3478));
// 			servers.Add(("stun.whoi.edu", 3478));
// 			servers.Add(("stun.wifirst.net", 3478));
// 			servers.Add(("stun.wwdl.net", 3478));
// 			servers.Add(("stun.xs4all.nl", 3478));
// 			servers.Add(("stun.xtratelecom.es", 3478));
// 			servers.Add(("stun.yesss.at", 3478));
// 			servers.Add(("stun.zadarma.com", 3478));
// 			servers.Add(("stun.zadv.com", 3478));
// 			servers.Add(("stun.zoiper.com", 3478));
// 			servers.Add(("stun1.faktortel.com.au", 3478));
//
// 		#endregion
//
// 		}
//
// 		public Task<QueryResult> QueryAddress() {
// 			return this.QueryAddressAsync();
// 		}
//
// 		public Task<QueryResult> QueryAddressAsync(Action<QueryResult> callback = null) {
//
// 			Task<QueryResult> task = Task.Run(() => {
// 				QueryResult result = new QueryResult();
//
// 				foreach((string server, int port) server in servers) {
//
// 					IPAddress serverIp = Dns.GetHostEntry(server.server).AddressList.First();
// 					IPEndPoint serverEndPoint = new IPEndPoint(serverIp, server.port);
//
// 					Console.WriteLine("Querying public IP address...");
//
// 					STUNQueryResult queryResult = STUN.STUNClient.Query(serverEndPoint, STUNQueryType.ExactNAT, true);
//
// 					if(queryResult.QueryError == STUNQueryError.Success) {
// 						result.SuccessResults = queryResult;
//
// 						if(callback != null) {
// 							callback(result);
// 						}
//
// 						break;
// 					}
//
// 					result.errors.Add(queryResult);
// 				}
//
// 				return result;
// 			});
//
// 			return task;
// 		}
//
// 		public class QueryResult {
// 			public readonly List<STUNQueryResult> errors = new List<STUNQueryResult>();
//
// 			public STUNQueryResult SuccessResults;
// 		}
// 	}
//
// }