using MongoDB.Driver;
using DotnetBackend.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DotnetBackend.Services;

public class WithdrawalService
{
    private readonly IMongoCollection<Withdrawal> _withdrawals;
    private readonly CounterService _counterService;
    private readonly ExtractService _extractService;
    private readonly ClientService _clientService;
    private readonly PurchaseService _purchaseService;
    private readonly BankAccountService _bankAccountService;
    private readonly SystemConfigService _systemConfigService;
    public WithdrawalService(MongoDbService mongoDbService, ClientService clientService,
    CounterService counterService, ExtractService extractService, PurchaseService purchaseService,
     BankAccountService bankAccountService, SystemConfigService systemConfigService)
    {
        _withdrawals = mongoDbService.GetCollection<Withdrawal>("Withdrawal");
        _counterService = counterService;
        _extractService = extractService;
        _clientService = clientService;
        _purchaseService = purchaseService;
        _bankAccountService = bankAccountService;
        _systemConfigService = systemConfigService;
    }

    public async Task<Withdrawal> CreateWithdrawalAsync(Withdrawal withdrawal)
    {

        if (withdrawal.DateCreated == null)
        {
            TimeZoneInfo brasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            DateTime utcNow = DateTime.UtcNow;
            DateTime brasiliaTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, brasiliaTimeZone);
            withdrawal.DateCreated = brasiliaTime;
        }

        withdrawal.WithdrawalId = "W" + await _counterService.GetNextSequenceAsync("withdraw");
        withdrawal.AmountWithdrawnReceivable = withdrawal.AmountWithdrawn - (withdrawal.AmountWithdrawn*0.04);

        Client? client = await _clientService.GetClientByIdAsync(withdrawal.ClientId);

        if (client == null)
        {
            throw new InvalidOperationException("Cliente não encontrado.");
        }

        var valorASerRetirado = (decimal)withdrawal.AmountWithdrawn;

        if (withdrawal.WithdrawnItems == null)
        {
            withdrawal.WithdrawnItems = new List<string>();
        }

        if (client.ExtraBalance >= valorASerRetirado)
        {
            await _clientService.WithdrawFromExtraBalanceAsync(withdrawal.ClientId, valorASerRetirado);
            withdrawal.WithdrawnItems.Add("Extra Balance");
            valorASerRetirado = 0;

        }else if (client.ExtraBalance > 0)
        {
            valorASerRetirado -= (decimal)client.ExtraBalance;
            await _clientService.WithdrawFromExtraBalanceAsync(withdrawal.ClientId, (decimal)client.ExtraBalance);
            withdrawal.WithdrawnItems.Add("Extra Balance");
        }

        if (valorASerRetirado > 0)
        {
            List<Purchase> purchases = await _purchaseService.GetPurchasesByClientIdAsync(withdrawal.ClientId);
            foreach (Purchase purchase in purchases)
            {
                if (valorASerRetirado > 0)
                {
                    Console.WriteLine($"Analisando contrato {purchase.PurchaseId} para realizar retirada");
                    if (purchase.Status == 2 || purchase.Status == 3)
                    {
                        int? diasPraSacar = purchase.DaysToFirstWithdraw;
                        int diasPraSacarVal = 90;
                        if (diasPraSacar != null)
                        {
                            diasPraSacarVal = (int)diasPraSacar;
                        }
                        DateTime dataLimite = DateTime.Now.AddDays(-diasPraSacarVal);

                        if ((purchase.FirstIncreasement.HasValue && purchase.FirstIncreasement < dataLimite) || (bool)purchase.FreeWithdraw)
                        {
                            if ((purchase.CurrentIncome - purchase.AmountWithdrawn) >= valorASerRetirado)
                            {
                                withdrawal.WithdrawnItems.Add(purchase.PurchaseId);
                                await _purchaseService.WithdrawFromPurchaseAsync(purchase.PurchaseId, valorASerRetirado);
                                await _clientService.WithdrawFromBalanceAsync(withdrawal.ClientId, valorASerRetirado);
                                valorASerRetirado = 0;
                            }
                            else if ((purchase.CurrentIncome - purchase.AmountWithdrawn) > 0)
                            {
                                valorASerRetirado -= (purchase.CurrentIncome - purchase.AmountWithdrawn);
                                withdrawal.WithdrawnItems.Add(purchase.PurchaseId);
                                await _clientService.WithdrawFromBalanceAsync(withdrawal.ClientId, purchase.CurrentIncome - purchase.AmountWithdrawn);
                                await _purchaseService.WithdrawFromPurchaseAsync(purchase.PurchaseId, purchase.CurrentIncome - purchase.AmountWithdrawn);
                            }
                        }
                    }
                }
            }
        }

        var stringExtrato = "";
        foreach (string aiaiPapai in withdrawal.WithdrawnItems)
        {
            stringExtrato += aiaiPapai + "-";
        }

        var extract = new Extract($"Saque da Carteira, retirada de ${stringExtrato}", (decimal)withdrawal.AmountWithdrawn, withdrawal.ClientId);
        await _extractService.CreateExtractAsync(extract);
        await _clientService.AddWithdrawalAsync(withdrawal.ClientId, withdrawal.WithdrawalId);
        await _withdrawals.InsertOneAsync(withdrawal);
        return withdrawal;
    }


    public async Task<Withdrawal?> GetWithdrawalByIdAsync(string id)
    {
        var normalizedId = id.Trim();
        return await _withdrawals.Find(p => p.WithdrawalId == normalizedId).FirstOrDefaultAsync();
    }

    public async Task<List<Withdrawal>> GetAllWithdrawalsAsync()
    {
        return await _withdrawals.Find(_ => true).ToListAsync(); // Retorna todos os saques
    }

    public async Task<List<Withdrawal>> GetAllWithdrawalsFilteredAsync(int dateFilter)
    {
        if (dateFilter < 0)
        {
            throw new ArgumentException("O filtro de data deve ser um número positivo representando a quantidade de dias.");
        }
        var cutoffDate = DateTime.UtcNow.AddDays(-dateFilter);

        return await _withdrawals.Find(wi => wi.DateCreated >= cutoffDate).ToListAsync();
    }

    public async Task<List<Withdrawal>> GetAllWithdrawsToPay()
    {
        return await _withdrawals.Find(w => w.Status == 1).ToListAsync();
    }

    public async Task<bool> DeleteWithdrawalAsync(string id)
    {
        var normalizedId = id.Trim();
        var saqueEncontrado = await _withdrawals.Find(p => p.WithdrawalId == normalizedId).FirstOrDefaultAsync();
        Console.WriteLine($"saqueEncontrado: {saqueEncontrado}");

        if (saqueEncontrado == null)
        {
            Console.WriteLine($"Withdrawal with ID {normalizedId} not found.");
            return false; // Saque não encontrado, retorna falso
        }

        var deleteResult = await _withdrawals.DeleteOneAsync(w => w.WithdrawalId == normalizedId);
        Console.WriteLine($"deleteResult: {deleteResult}");
        return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
    }

    public async Task<bool> UpdateStatus(string withdrawalId, int newStatus)
    {
        var existingWithdrawal = await GetWithdrawalByIdAsync(withdrawalId);

        if (existingWithdrawal != null)
        {
            try
            {
                // if (newStatus == 2)
                // {
                //     await _bankAccountService.WithdrawFromBalanceAsync(withdrawalId, (decimal)existingWithdrawal.AmountWithdrawn);
                // }

                if (newStatus == 3)
                {
                    Console.WriteLine("Cancelando");
                    await _clientService.AddToExtraBalanceAsync(existingWithdrawal.ClientId, (decimal)existingWithdrawal.AmountWithdrawn);
                    var extract = new Extract($"Saque {existingWithdrawal.WithdrawalId} Negado, Valor de R${existingWithdrawal.AmountWithdrawn} adicionado à carteira ", (decimal)existingWithdrawal.AmountWithdrawn, existingWithdrawal.ClientId);
                    await _extractService.CreateExtractAsync(extract);
                }

                existingWithdrawal.Status = newStatus;
                var updateDefinition = Builders<Withdrawal>.Update.Set(w => w.Status, newStatus);
                await _withdrawals.UpdateOneAsync(w => w.WithdrawalId == withdrawalId, updateDefinition);
                Console.WriteLine($"Saque encontrado: {existingWithdrawal.WithdrawalId}, novo status {newStatus}");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar status ou processar a retirada: {ex.Message}");
                return false; // Retorna falso se houve um erro
            }
        }
        else
        {
            Console.WriteLine($"Erro ao encontrar saque {withdrawalId}");
            return false;
        }
    }

}