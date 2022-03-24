using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.Indexer.Cirrus.Client;
using Blockcore.Indexer.Cirrus.Storage.Mongo.SmartContracts.Dao;
using Blockcore.Indexer.Cirrus.Storage.Mongo.Types;
using Blockcore.Indexer.Core.Client;
using Blockcore.Indexer.Core.Operations.Types;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Blockcore.Indexer.Cirrus.Storage.Mongo.SmartContracts;

public class DaoContractAggregator : IDAOContractAggregator
{
   const string DaoContract = "DAOContract";

   readonly ILogger<DaoContractAggregator> logger;
   readonly ICirrusMongoDb mongoDb;
   readonly ILogReaderFactory logReaderFactory;
   readonly CirrusClient cirrusClient;

   public DaoContractAggregator(ILogger<DaoContractAggregator> logger, ICirrusMongoDb mongoData, ILogReaderFactory logReaderFactory, ICryptoClientFactory clientFactory, SyncConnection connection)
   {
      mongoDb = mongoData;
      this.logReaderFactory = logReaderFactory;
      this.logger = logger;

      cirrusClient = clientFactory.Create(connection) as CirrusClient;
   }

   public async Task<DaoContractComputedTable> ComputeDaoContractForAddressAsync(string address)
   {
      var contract = await LookupDaoContractForAddressAsync(address);

      if (contract is null)
         return null;

      var contractTransactions = await mongoDb.CirrusContractTable
         .AsQueryable()
         .Where(_ => _.ToAddress == address && _.Success && _.BlockIndex > contract.LastProcessedBlockHeight)
         .ToListAsync();

      if (contractTransactions.Any())
      {
         await AddNewTransactionsDataToDocumentAsync(address, contractTransactions, contract);
      }

      return contract;
   }

   async Task<DaoContractComputedTable> LookupDaoContractForAddressAsync(string address)
   {
      DaoContractComputedTable contract = await mongoDb.DaoContractComputedTable
         .AsQueryable()
         .SingleOrDefaultAsync(_ => _.ContractAddress == address);

      if (contract is not null)
         return contract;

      var contractCode = await mongoDb.CirrusContractCodeTable
         .AsQueryable()
         .SingleOrDefaultAsync(_ => _.ContractAddress == address);

      if (contractCode is null || contractCode.CodeType != DaoContract)
      {
         logger.LogInformation(
            $"Request to compute DAO contract for address {address} which was not found in the contract code table");
         return null;
      }

      contract = await CreateNewDaoContract(address);

      if (contract is null)
         throw new ArgumentNullException($"Contract not found in the contract table for address {address}");

      return contract;
   }

   private async Task<DaoContractComputedTable> CreateNewDaoContract(string address)
   {
      var contractCreationTransaction = await mongoDb.CirrusContractTable
         .AsQueryable()
         .Where(_ => _.NewContractAddress == address)
         .SingleOrDefaultAsync();

      if (contractCreationTransaction is null)
         throw new ArgumentNullException(nameof(contractCreationTransaction));

      var contract = new DaoContractComputedTable
      {
         ContractAddress = contractCreationTransaction.NewContractAddress,
         ContractCreateTransactionId = contractCreationTransaction.TransactionId,
         LastProcessedBlockHeight = contractCreationTransaction.BlockIndex
      };

      await SaveTheContractAsync(address, contract);

      return contract;
   }

   private async Task AddNewTransactionsDataToDocumentAsync(string address, List<CirrusContractTable> contractTransactions,
      DaoContractComputedTable contract)
   {
      foreach (var contractTransaction in contractTransactions)
      {
         var reader = logReaderFactory.GetLogReader(contractTransaction.MethodName);

         if (reader is null)
         {
            logger.LogInformation($"No reader found for method {contractTransaction.MethodName} on transaction id - {contractTransaction.TransactionId}");
            continue; //TODO need to verify this is the right way to go
         }

         if (!reader.IsTransactionLogComplete(contractTransaction.Logs))
         {
            var result = await cirrusClient.GetContractInfoAsync(contractTransaction.TransactionId);

            contractTransaction.Logs = result.Logs;
         }

         reader.UpdateContractFromTransactionLog(contractTransaction, contract);

         contract.LastProcessedBlockHeight = contractTransaction.BlockIndex;
      }

      await SaveTheContractAsync(address, contract);
   }

   async Task SaveTheContractAsync(string address, DaoContractComputedTable contract) =>
      await mongoDb.DaoContractComputedTable.FindOneAndReplaceAsync<DaoContractComputedTable>(
         _ => _.ContractAddress == address, contract,
         new FindOneAndReplaceOptions<DaoContractComputedTable> { IsUpsert = true },
         CancellationToken.None);
}
