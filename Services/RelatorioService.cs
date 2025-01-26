using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Font;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotnetBackend.Models;
using iText.IO.Font.Constants;

namespace DotnetBackend.Services;

public class RelatorioService
{
    private readonly ClientService _clientService;
    public RelatorioService(ClientService clientService)
    {
        _clientService = clientService;
    }

    public async Task<MemoryStream> GenerateClientReportPdfAsync(List<Client> clients)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var pdfWriter = new PdfWriter(memoryStream))
            {
                using (var pdf = new PdfDocument(pdfWriter))
                {
                    var document = new Document(pdf);

                    var boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                    document.Add(new Paragraph("Relatório de Clientes")
                        .SetFont(boldFont)
                        .SetFontSize(20)
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

                    var table = new Table(new float[] { 3, 3, 3, 3 })
                        .SetWidth(UnitValue.CreatePercentValue(100));

                    table.AddHeaderCell("Nome");
                    table.AddHeaderCell("ID");
                    table.AddHeaderCell("Data de Cadastro");
                    table.AddHeaderCell("Telefone");

                    foreach (var client in clients)
                    {
                        table.AddCell(client.Name);
                        table.AddCell(client.Id);
                        table.AddCell(client.DateCreated.ToString("dd/MM/yyyy"));
                        table.AddCell(client.Phone);
                    }

                    document.Add(table);
                    document.Close();
                }
            }

            return memoryStream;
        }
    }

}