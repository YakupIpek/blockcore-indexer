using System.Net;
using System.Threading.Tasks;
using Blockcore.Indexer.Client;
using Blockcore.Indexer.Client.Types;
using Cirrus.Client.Types;
using Cirrus.Storage.Types;

namespace Cirrus.Client
{
   public class CirrusClient : BitcoinClient
   {
      public CirrusClient(string uri, NetworkCredential credentials)
      : base(uri,credentials)
      { }

      public CirrusClient(string connection, int port, string user, string pass, bool secure)
      : base(string.Format("{0}://{1}:{2}", secure ? "https" : "http", connection, port), new NetworkCredential(user, pass))
      { }
      public override async Task<BlockInfo> GetBlockAsync(string hash)
      {
         return await CallAsync<CirrusBlockInfo>("getblock", hash);
      }

   }
}
