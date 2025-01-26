using MongoDB.Driver;
using DotnetBackend.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotnetBackend.Services
{
    public class ReservaService
    {
        private readonly IMongoCollection<Reserva> _reservas;
        private readonly ClientService _clientService;
        private readonly CounterService _counterService;
        private readonly ChaleService _chaleService;
        private readonly ExtractService _extractService;

        public ReservaService(MongoDbService mongoDbService, ClientService clientService,
        ChaleService chaleService, CounterService counterService, ExtractService extractService)
        {
            _reservas = mongoDbService.GetCollection<Reserva>("Reservas");
            _clientService = clientService;
            _counterService = counterService;
            _chaleService = chaleService;
            _extractService = extractService;
        }

        public async Task<List<Reserva>> GetAllReservasAsync()
        {
            return await _reservas.Find(_ => true).ToListAsync();
        }

        public async Task<bool> DeleteReservaAsync(string id)
        {
            var deleteResult = await _reservas.DeleteOneAsync(c => c.ReservaId == id);
            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<Reserva?> GetReservaByIdAsync(string id)
        {
            var normalizedId = id.Trim();
            return await _reservas.Find(c => c.ReservaId == normalizedId).FirstOrDefaultAsync();
        }

        public async Task<Reserva> CreateReservaAsync(Reserva reserva)
        {
            if (string.IsNullOrWhiteSpace(reserva.ClientId))
            {
                throw new ArgumentException("O ClientId deve ser fornecido.");
            }

            var existingClient = await _clientService.GetClientByIdAsync(reserva.ClientId);
            if (existingClient == null)
            {
                throw new InvalidOperationException("Cliente não encontrado. Verifique o ClientId.");
            }

            reserva.ClientName = existingClient.Name;

            var overlappingReservas = await _reservas.Find(r =>
                r.Checkin < reserva.Checkout && r.Checkout > reserva.Checkin)
                .ToListAsync();

            foreach (var existingReserva in overlappingReservas)
            {
                if (existingReserva.ChaleIds != null &&
                    existingReserva.ChaleIds.Intersect(reserva.ChaleIds ?? new List<string>()).Any() && existingClient.Status == 2)
                {
                    throw new InvalidOperationException("Não é possível criar a reserva. Chalé(s) reservado(s) no período solicitado.");
                }
            }

            await _reservas.InsertOneAsync(reserva);
            return reserva;
        }

        public async Task<bool> DeleteAllReservasAsync()
        {
            var deleteResult = await _reservas.DeleteManyAsync(_ => true);
            return deleteResult.IsAcknowledged && deleteResult.DeletedCount > 0;
        }

        public async Task<List<Reserva>> GetReservasByPeriodAsync(DateTime startDate, DateTime endDate)
        {
            var reservas = await _reservas.Find(r =>
                r.Checkin < endDate.Date && r.Checkout > startDate.Date)
                .ToListAsync();

            return reservas;
        }

        // public async Task<List<Chale>> GetChalesDisponiveisAsync(DateTime startDate, DateTime endDate)
        // {
        //     var reservas = await _reservas.Find(r =>
        //         r.Checkin < endDate.Date && r.Checkout > startDate.Date)
        //         .ToListAsync();

        //     var chales = await _chaleService.GetAllChalesAsync();

        //     var chaletsIndisponiveis = new HashSet<string>();

        //     foreach (var reserva in reservas)
        //     {
        //         foreach (var chaleId in reserva.ChaleIds)
        //         {
        //             if (reserva.Checkin < endDate.Date && reserva.Checkout > startDate.Date)
        //             {
        //                 chaletsIndisponiveis.Add(chaleId.ToLower().Trim());
        //             }
        //         }
        //     }

        //     var chalesDisponiveis = chales.Where(chale => !chaletsIndisponiveis.Contains(chale.Name.ToLower().Trim())).ToList();

        //     return chalesDisponiveis;
        // }

        public async Task<List<ChaleDisponivel>> GetChalesDisponiveisAsync(DateTime startDate, DateTime endDate)
        {
            var reservas = await _reservas.Find(r =>
                r.Checkin < endDate.Date && r.Checkout > startDate.Date && r.Status == 2)
                .ToListAsync();

            var chales = await _chaleService.GetAllChalesAsync();
            var chaletsIndisponiveis = new HashSet<string>();

            foreach (var reserva in reservas)
            {
                foreach (var chaleId in reserva.ChaleIds)
                {
                    if (reserva.Checkin < endDate.Date && reserva.Checkout > startDate.Date)
                    {
                        chaletsIndisponiveis.Add(chaleId.ToLower().Trim());
                    }
                }
            }

            var chalesDisponiveis = new List<ChaleDisponivel>();

            foreach (var chale in chales)
            {
                var chaleId = chale.Name.ToLower().Trim();
                if (!chaletsIndisponiveis.Contains(chaleId))
                {
                    chalesDisponiveis.Add(new ChaleDisponivel { Name = chale.Name, Status = 1 });
                }
                else
                {
                    // var temCheckoutNoDia = reservas.Any(r => r.ChaleIds.Contains(chaleId) &&
                    //                                           r.Checkout?.Date == startDate.Date.Date);

                    var dataComoString = startDate.ToString("yyyy-MM-dd");
                    var partesData = dataComoString.Split(" ")[0];

                    var temCheckoutNoDia = false;
                    Reserva reservaAux = null;

                    foreach (var r in reservas)
                    {
                        var dataCheckoutComoString = r.Checkout?.ToString("yyyy-MM-dd");
                        var partesDataCheckout = dataCheckoutComoString.Split(" ")[0];

                        if (partesDataCheckout == partesData)
                        {
                            temCheckoutNoDia = true;
                            reservaAux = r;
                        }
                    }

                    if (temCheckoutNoDia)
                    {
                        chalesDisponiveis.Add(new ChaleDisponivel { Name = chale.Name, Status = 2, ClientName = reservaAux.ClientName, ReservaId = reservaAux.ReservaId });
                    }
                }
            }

            return chalesDisponiveis;
        }

        public async Task<bool> UpdateReservaStatusAsync(string reservaId, int newStatus)
        {
            if (newStatus < 1 || newStatus > 4)
            {
                throw new ArgumentException("Status deve ser um valor entre 1 e 4.");
            }

            var reserva = await _reservas.Find(r => r.ReservaId == reservaId).FirstOrDefaultAsync();
            if (reserva == null)
            {
                throw new InvalidOperationException("Reserva não encontrada.");
            }

            reserva.Status = newStatus;

            var updateDefinition = Builders<Reserva>.Update.Set(r => r.Status, newStatus);

            var result = await _reservas.UpdateOneAsync(r => r.ReservaId == reservaId, updateDefinition);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> AddPayment(string reservaId, decimal payment)
        {
            if (payment <= 0)
            {
                throw new ArgumentException("O valor do pagamento tem que ser maior que 0.");
            }

            var reserva = await _reservas.Find(r => r.ReservaId == reservaId).FirstOrDefaultAsync();
            if (reserva == null)
            {
                throw new InvalidOperationException("Reserva não encontrada.");
            }

            var valorAtual = reserva.AmountPaid;
            var novoValor = valorAtual + payment;

            reserva.AmountPaid = novoValor;

            // Aqui você cria uma definição de atualização para mudar o status
            var updateDefinition = Builders<Reserva>.Update
                .Set(r => r.AmountPaid, novoValor)
                .Set(r => r.Status, 2); // Altera o status para 2

            var extract = new Extract($"Pagamento de ${payment} referente a reserva {reservaId}", novoValor, reserva.ClientId);
            await _extractService.CreateExtractAsync(extract);

            // Atualiza a reserva com o novo valor e novo status
            var result = await _reservas.UpdateOneAsync(r => r.ReservaId == reservaId, updateDefinition);

            return result.ModifiedCount > 0;
        }

        public async Task<bool> EditarValorTotal(string reservaId, decimal newValue)
        {
            if (newValue <= 0)
            {
                throw new ArgumentException("O valor do pagamento tem que ser maior que 0.");
            }

            var reserva = await _reservas.Find(r => r.ReservaId == reservaId).FirstOrDefaultAsync();
            if (reserva == null)
            {
                throw new InvalidOperationException("Reserva não encontrada.");
            }

            var savePay = reserva.TotalPrice;
            reserva.TotalPrice = newValue;

            var updateDefinition = Builders<Reserva>.Update
                .Set(r => r.TotalPrice, newValue);

            var extract = new Extract($"Valor Total da reserva alterado de R${savePay} para ${newValue} referente a reserva {reservaId}", newValue, reserva.ClientId);
            await _extractService.CreateExtractAsync(extract);

            var result = await _reservas.UpdateOneAsync(r => r.ReservaId == reservaId, updateDefinition);

            return result.ModifiedCount > 0;
        }

        public async Task<List<ReservedDateWithChales>> GetReservedDatesByMonthAsync(int year, int month)
        {
            var reservas = await _reservas.Find(r =>
               ( r.Checkin.HasValue && r.Checkin.Value.Year == year ||
                r.Checkout.HasValue && r.Checkout.Value.Year == year) && r.Status == 2)
                .ToListAsync();

            var reservedDatesWithChales = new List<ReservedDateWithChales>();

            foreach (var reserva in reservas)
            {
                if (reserva.Checkin.HasValue && reserva.Checkout.HasValue)
                {
                    var checkin = reserva.Checkin.Value;
                    var checkout = reserva.Checkout.Value;

                    for (var date = checkin.Date; date <= checkout.Date; date = date.AddDays(1))
                    {
                        if (date.Year == year && date.Month == month)
                        {
                            reservedDatesWithChales.Add(new ReservedDateWithChales
                            {
                                Data = date.ToString("yyyy-MM-dd"), 
                                Chales = reserva.ChaleIds
                            });
                        }
                    }
                }
            }

            return reservedDatesWithChales.Distinct().OrderBy(item => item.Data).ToList();
        }

        public async Task<List<Reserva>> GetReservedDatesByMonthYearAndDayAsync(int year, int month, int day)
        {
            var targetDate = new DateTime(year, month, day);

            var reservas = await _reservas.Find(r =>
                r.Checkin.HasValue && r.Checkout.HasValue && r.Status == 2 &&
                ((r.Checkin.Value.Date <= targetDate && r.Checkout.Value.Date > targetDate) || 
                (r.Checkout.Value.Date.Day == targetDate.Date.Day &&
                 r.Checkout.Value.Date.Month == targetDate.Date.Month && 
                 r.Checkout.Value.Date.Year == targetDate.Date.Year)) 
            ).ToListAsync();

            return reservas;
        }

        public async Task<decimal> GetTotalValueByMonthAsync(int year, int month)
        {
            var reservas = await _reservas.Find(r =>
                (r.Checkin.HasValue && r.Checkin.Value.Year == year && r.Checkin.Value.Month == month &&
                 (r.Status == 2 || r.Status == 3)) ||
                (r.Checkout.HasValue && r.Checkout.Value.Year == year && r.Checkout.Value.Month == month &&
                 (r.Status == 2 || r.Status == 3)))
                .ToListAsync();

            decimal totalValue = reservas.Sum(r => r.AmountPaid);

            return totalValue;
        }

        public async Task<decimal> GetTotalValueForCurrentYearAsync()
        {
            var currentYear = DateTime.UtcNow.Year;

            var reservas = await _reservas.Find(r =>
                ((r.Checkin.HasValue && r.Checkin.Value.Year == currentYear) ||
                 (r.Checkout.HasValue && r.Checkout.Value.Year == currentYear)) &&
                 (r.Status == 2 || r.Status == 3)).ToListAsync();

            decimal totalValue = reservas.Sum(r => r.AmountPaid);

            return totalValue;
        }

        public async Task<object> GetReservaCountsAsync()
        {
            var today = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")); // Convertendo para horário de Brasília
            var startOfToday = today.Date;
            var endOfToday = startOfToday.AddDays(1);

            var dailyCount = await _reservas.CountDocumentsAsync(r =>
                r.Checkin.HasValue && r.Checkin.Value < endOfToday &&
                r.Checkout.HasValue && r.Checkout.Value > startOfToday);

            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            var monthlyCount = await _reservas.CountDocumentsAsync(r =>
                r.Checkin.HasValue && r.Checkin.Value < endOfMonth &&
                r.Checkout.HasValue && r.Checkout.Value > startOfMonth);

            var statusCount = await _reservas.CountDocumentsAsync(r => r.Status == 1);

            return new
            {
                ReservasHoje = dailyCount,
                ReservasEsteMes = monthlyCount,
                ReservasStatus1 = statusCount
            };
        }

        public async Task<string> EditarClientNameByClientIdAsync(string clientId, string newName)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("O ClientId deve ser fornecido.");
            }

            var reservas = await _reservas.Find(r => r.ClientId == clientId).ToListAsync();

            if (!reservas.Any())
            {
                return "Ok";
            }

            var updateDefinition = Builders<Reserva>.Update.Set(r => r.ClientName, newName);
            await _reservas.UpdateManyAsync(r => r.ClientId == clientId, updateDefinition);

            return "Ok";
        }
    }
}