using System;
using System.Linq;
using Blockcore.Indexer.Cirrus.Client.Types;
using Blockcore.Indexer.Cirrus.Storage.Mongo.Types;

namespace Blockcore.Indexer.Cirrus.Storage.Mongo.SmartContracts.NonFungibleTokenStore;

public class CancelSaleLogReader : ILogReader<NonFungibleTokenComputedTable>
{
   public bool CanReadLogForMethodType(string methodType) => methodType.Equals("CancelSale");

   public bool IsTransactionLogComplete(LogResponse[] logs) => logs is { Length: 2 };

   public void UpdateContractFromTransactionLog(CirrusContractTable contractTransaction,
      NonFungibleTokenComputedTable computedTable)
   {
      var transferLog = contractTransaction.Logs[0];
      var tokenPurchaseLog = contractTransaction.Logs[1];

      string tokenId = (string) transferLog.Log.Data["tokenId"];

      var token = computedTable.Tokens.Single(_ => _.Id == tokenId);

      var saleEvent = (OnSale) token.SalesHistory.Last(_ => _ is OnSale);

      if (saleEvent.Seller != contractTransaction.FromAddress)
         throw new InvalidOperationException($"The seller must cancel the sale order {saleEvent.TransactionId} for {contractTransaction.TransactionId}");

      token.SalesHistory.Remove(saleEvent);
   }
}
