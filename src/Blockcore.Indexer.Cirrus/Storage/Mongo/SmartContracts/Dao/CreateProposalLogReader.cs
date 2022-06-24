using System.Collections.Generic;
using System.Linq;
using Blockcore.Indexer.Cirrus.Client.Types;
using Blockcore.Indexer.Cirrus.Storage.Mongo.Types;
using MongoDB.Driver;

namespace Blockcore.Indexer.Cirrus.Storage.Mongo.SmartContracts.Dao;

class CreateProposalLogReader : ILogReader<DaoContractTable,DaoContractProposalTable>
{
   public bool CanReadLogForMethodType(string methodType) => methodType == "CreateProposal";

   public bool IsTransactionLogComplete(LogResponse[] logs) => true;

   public WriteModel<DaoContractProposalTable>[] UpdateContractFromTransactionLog(CirrusContractTable contractTransaction,
      DaoContractTable computedTable)
   {
      var logData = contractTransaction.Logs.First().Log.Data;

      var proposal = new DaoContractProposalTable
      {
         Recipient = (string)logData["recipent"],
         Amount = (long)logData["amount"],
         Id = new SmartContractTokenId
         {
            TokenId = ((long)logData["proposalId"]).ToString(),
            ContractAddress = computedTable.ContractAddress
         } ,
         Description = (string)logData["description"],
         ProposalStartedAtBlock = contractTransaction.BlockIndex,
         Votes = new List<DaoContractVoteDetails>()
      };

      return new [] { new InsertOneModel<DaoContractProposalTable>(proposal)};
   }
}
