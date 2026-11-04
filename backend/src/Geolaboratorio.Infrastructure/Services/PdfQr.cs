using System.Security.Cryptography;
using System.Text;
using Geolaboratorio.Application;
using Geolaboratorio.Domain;
using Microsoft.Extensions.Options;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Geolaboratorio.Infrastructure.Services;

public class QrService(IOptions<AppOptions> opt) : IQrService
{
    public string BuildUrl(string folio) => $"{opt.Value.VerificationBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(folio)}";

    public byte[] GeneratePng(string folio)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(BuildUrl(folio), QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(12);
    }
}

public class PdfService(IOptions<AppOptions> opt) : IPdfService
{
    private const string Blue = "#0A2A8F";
    private const string Dark = "#2B2F36";
    private const string Light = "#EEF0F3";
    private readonly AppOptions _o = opt.Value;

    static PdfService() => QuestPDF.Settings.License = LicenseType.Community;

    private static byte[]? ReadIf(string? pathOrBase64)
    {
        if (string.IsNullOrWhiteSpace(pathOrBase64)) return null;
        try
        {
            if (pathOrBase64.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                var commaIdx = pathOrBase64.IndexOf(',');
                if (commaIdx > 0) return Convert.FromBase64String(pathOrBase64[(commaIdx + 1)..]);
            }
            if (File.Exists(pathOrBase64)) return File.ReadAllBytes(pathOrBase64);
        }
        catch { /* ignorar imagen corrupta para no interrumpir la generación del PDF */ }
        return null;
    }

    /// <summary>Sello digital: huella de los datos esenciales (base para firma electrónica avanzada futura).</summary>
    public static string Sello(Certificado c)
    {
        var raw = $"{c.Folio}|{c.Equipo?.SeriePrincipal}|{c.FechaCalibracion:yyyyMMdd}|{c.FechaVencimiento:yyyyMMdd}|{c.Resultado}|{c.Version}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    public byte[] Generate(Certificado c, byte[] qrPng)
    {
        var logo = ReadIf(_o.LogoPath);
        var firmaTec = ReadIf(c.Tecnico?.FirmaUrl);
        var firmaResp = ReadIf(_o.ResponsableFirmaPath);
        var eq = c.Equipo!; var cl = eq.Cliente!;
        var cancelado = c.Estatus == EstatusCertificado.Cancelado;
        var es = new System.Globalization.CultureInfo("es-MX");
        string F(DateTime d) => d.ToString("dd 'de' MMMM 'de' yyyy", es);
        var tituloDoc = c.TipoRegistro == "Certificación" ? "CERTIFICADO DE CERTIFICACIÓN TÉCNICA" : "CERTIFICADO DE CALIBRACIÓN";

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(9.5f).FontColor(Dark).FontFamily("Arial"));

            page.Header().Column(h =>
            {
                h.Item().Row(r =>
                {
                    if (logo != null) r.RelativeItem().Height(50).AlignLeft().Image(logo).FitArea();
                    else r.RelativeItem().Height(50).AlignMiddle().Text("GEOLABORATORIO").Bold().FontSize(20).FontColor(Blue);
                    r.ConstantItem(240).AlignRight().Column(x =>
                    {
                        x.Item().AlignRight().Text(tituloDoc).Bold().FontSize(12).FontColor(Blue);
                        x.Item().AlignRight().Text($"Folio: {c.Folio}").Bold().FontSize(11);
                        x.Item().AlignRight().Text($"Versión {c.Version}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
                h.Item().PaddingTop(6).LineHorizontal(2).LineColor(Blue);
            });

            page.Content().PaddingVertical(12).Column(col =>
            {
                col.Spacing(10);

                col.Item().Element(e => Section(e, "DATOS DEL CLIENTE", t =>
                {
                    Row(t, "Razón social", cl.RazonSocial); Row(t, "RFC", cl.RFC);
                    Row(t, "Dirección", string.Join(", ", new[] { cl.Direccion, cl.Ciudad, cl.Estado, cl.Pais }.Where(s => !string.IsNullOrWhiteSpace(s))));
                }));

                col.Item().Element(e => Section(e, "DATOS DEL EQUIPO", t =>
                {
                    Row(t, "Tipo de equipo", Mapper.TipoNombre(eq.TipoEquipo)); Row(t, "Marca / Modelo", $"{eq.Marca} {eq.Modelo}");
                    Row(t, "No. de serie", eq.SeriePrincipal);
                    if (!string.IsNullOrEmpty(eq.SerieSecundaria)) Row(t, "Serie secundaria", eq.SerieSecundaria);
                    if (!string.IsNullOrEmpty(eq.IMEI1)) Row(t, "IMEI 1", eq.IMEI1);
                    if (!string.IsNullOrEmpty(eq.IMEI2)) Row(t, "IMEI 2", eq.IMEI2);
                    if (!string.IsNullOrEmpty(eq.NumeroInterno)) Row(t, "No. interno", eq.NumeroInterno);
                    foreach (var s in eq.BuildSeriesList().Where(s => s.TipoSerie is not ("Serie Principal" or "Serie Secundaria" or "IMEI 1" or "IMEI 2")))
                        Row(t, s.TipoSerie, s.Valor);
                }));

                col.Item().Element(e => Section(e, "FECHAS", t =>
                {
                    Row(t, "Fecha de calibración", F(c.FechaCalibracion)); Row(t, "Fecha de emisión", F(c.FechaEmision));
                    Row(t, "Fecha de vencimiento", $"{F(c.FechaVencimiento)}  (vigencia {c.VigenciaMeses} meses)");
                    Row(t, "Técnico responsable", c.Tecnico?.Nombre ?? "");
                    if (c.OrdenServicio != null) Row(t, "Orden de servicio", c.OrdenServicio.Folio);
                }));

                col.Item().Element(e => Section(e, "RESULTADOS", t =>
                {
                    t.Item().Text(string.IsNullOrWhiteSpace(c.Resultados) ? "—" : c.Resultados);
                }));
                if (!string.IsNullOrWhiteSpace(c.Observaciones))
                    col.Item().Element(e => Section(e, "OBSERVACIONES", t => t.Item().Text(c.Observaciones)));

                col.Item().Element(e => Section(e, "CONCLUSIÓN", t =>
                {
                    t.Item().Text(x =>
                    {
                        x.Span("Dictamen: ").SemiBold();
                        x.Span(c.Resultado.ToString().ToUpperInvariant()).Bold().FontColor(c.Resultado == ResultadoCalibracion.Rechazado ? Colors.Red.Darken2 : Blue);
                    });
                    if (!string.IsNullOrWhiteSpace(c.Conclusiones)) t.Item().PaddingTop(3).Text(c.Conclusiones);
                }));

                col.Item().PaddingTop(14).Row(r =>
                {
                    r.RelativeItem().Element(e => Firma(e, firmaTec, c.Tecnico?.Nombre ?? "", "Técnico responsable"));
                    r.ConstantItem(20);
                    r.RelativeItem().Element(e => Firma(e, firmaResp, _o.ResponsableNombre, _o.ResponsablePuesto));
                    r.ConstantItem(20);
                    r.ConstantItem(92).Column(q =>
                    {
                        q.Item().Image(qrPng).FitWidth();
                        q.Item().AlignCenter().Text("Verifique este certificado").FontSize(7);
                    });
                });
            });

            page.Footer().Column(f =>
            {
                f.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                f.Item().PaddingTop(3).Text($"Sello digital SHA-256: {Sello(c)}").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                f.Item().Row(r =>
                {
                    r.RelativeItem().Text("GEOLABORATORIO · Laboratorio de calibración de equipos topográficos · geolaboratorio.com.mx").FontSize(7);
                    r.ConstantItem(80).AlignRight().Text(x => { x.Span("Pág. ").FontSize(7); x.CurrentPageNumber().FontSize(7); x.Span("/").FontSize(7); x.TotalPages().FontSize(7); });
                });
            });

            if (cancelado)
                page.Background().AlignCenter().AlignMiddle().Rotate(-35)
                    .Text("CANCELADO").FontSize(110).Bold().FontColor(Colors.Red.Lighten4);
        })).GeneratePdf();
    }

    public byte[] GenerateReporteTecnico(OrdenServicio os, byte[] qrPng)
    {
        var logo = ReadIf(_o.LogoPath);
        var eq = os.Equipo!;
        var cl = os.Cliente ?? eq.Cliente!;
        var firmaTec = ReadIf(os.FirmaTecnico) ?? ReadIf(os.Tecnico?.FirmaUrl);
        var firmaSup = ReadIf(os.FirmaSupervisor) ?? ReadIf(_o.ResponsableFirmaPath);
        var firmaCli = ReadIf(os.FirmaCliente);
        var es = new System.Globalization.CultureInfo("es-MX");
        string F(DateTime d) => d.ToString("dd/MM/yyyy HH:mm", es);

        var accesorios = Mapper.ParseJsonList(os.AccesoriosChecklistJson);
        var condiciones = Mapper.ParseJsonList(os.CondicionChecklistJson);
        var series = eq.BuildSeriesList();

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(32);
            page.DefaultTextStyle(t => t.FontSize(9f).FontColor(Dark).FontFamily("Arial"));

            page.Header().Column(h =>
            {
                h.Item().Row(r =>
                {
                    if (logo != null) r.RelativeItem().Height(46).AlignLeft().Image(logo).FitArea();
                    else r.RelativeItem().Height(46).AlignMiddle().Text("GEOLABORATORIO").Bold().FontSize(18).FontColor(Blue);
                    r.ConstantItem(250).AlignRight().Column(x =>
                    {
                        x.Item().AlignRight().Text("REPORTE TÉCNICO DE ORDEN DE SERVICIO").Bold().FontSize(11).FontColor(Blue);
                        x.Item().AlignRight().Text($"Folio: {os.Folio}").Bold().FontSize(11);
                        x.Item().AlignRight().Text($"Estatus: {os.Estatus.ToUpperInvariant()} · Recepción: {F(os.FechaRecepcion)}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
                h.Item().PaddingTop(4).LineHorizontal(2).LineColor(Blue);
            });

            page.Content().PaddingVertical(10).Column(col =>
            {
                col.Spacing(8);

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => Section(e, "CLIENTE PROPIETARIO", t =>
                    {
                        Row(t, "Razón social", cl.RazonSocial);
                        Row(t, "RFC", cl.RFC);
                        if (!string.IsNullOrWhiteSpace(cl.Contacto)) Row(t, "Contacto", cl.Contacto);
                        if (!string.IsNullOrWhiteSpace(cl.Telefono)) Row(t, "Teléfono", cl.Telefono);
                    }));
                    row.ConstantItem(8);
                    row.RelativeItem().Element(e => Section(e, "EQUIPO TOPOGRÁFICO", t =>
                    {
                        Row(t, "Tipo", Mapper.TipoNombre(eq.TipoEquipo));
                        Row(t, "Marca / Modelo", $"{eq.Marca} {eq.Modelo}");
                        Row(t, "No. Interno", eq.NumeroInterno ?? "—");
                        Row(t, "Tipo de servicio", os.TipoServicio);
                    }));
                });

                col.Item().Element(e => Section(e, "SERIES REGISTRADAS DEL EQUIPO", t =>
                {
                    t.Item().Text(string.Join("   |   ", series.Select(s => $"{s.TipoSerie}: {s.Valor}")));
                }));

                col.Item().Row(row =>
                {
                    row.RelativeItem().Element(e => Section(e, "CONDICIÓN FÍSICA DE RECEPCIÓN", t =>
                    {
                        t.Item().Text(condiciones.Count > 0 ? string.Join(", ", condiciones) : "Sin daños reportados");
                        if (!string.IsNullOrWhiteSpace(os.FallaReportada))
                            t.Item().PaddingTop(3).Text($"Falla reportada: {os.FallaReportada}").Italic();
                    }));
                    row.ConstantItem(8);
                    row.RelativeItem().Element(e => Section(e, "ACCESORIOS RECIBIDOS", t =>
                    {
                        var accText = accesorios.Count > 0 ? string.Join(", ", accesorios) : "Ninguno";
                        if (!string.IsNullOrWhiteSpace(os.AccesoriosPersonalizados))
                            accText += $" ({os.AccesoriosPersonalizados})";
                        t.Item().Text(accText);
                    }));
                });

                col.Item().Element(e => Section(e, "DIAGNÓSTICO TÉCNICO", t =>
                {
                    t.Item().Text(string.IsNullOrWhiteSpace(os.Diagnostico) ? "Pendiente de diagnóstico." : os.Diagnostico);
                }));

                col.Item().Element(e => Section(e, "ACTIVIDADES REALIZADAS", t =>
                {
                    t.Item().Text(string.IsNullOrWhiteSpace(os.ActividadesRealizadas) ? "—" : os.ActividadesRealizadas);
                }));

                if (os.Refacciones.Count > 0)
                {
                    col.Item().Element(e => Section(e, "REFACCIONES UTILIZADAS", t =>
                    {
                        t.Item().Table(tbl =>
                        {
                            tbl.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(80);
                                c.RelativeColumn();
                                c.ConstantColumn(55);
                                c.ConstantColumn(75);
                                c.ConstantColumn(75);
                            });
                            tbl.Header(h =>
                            {
                                h.Cell().Text("SKU").Bold();
                                h.Cell().Text("Refacción / Descripción").Bold();
                                h.Cell().AlignRight().Text("Cant.").Bold();
                                h.Cell().AlignRight().Text("P. Unit.").Bold();
                                h.Cell().AlignRight().Text("Importe").Bold();
                            });
                            foreach (var refa in os.Refacciones)
                            {
                                tbl.Cell().PaddingVertical(2).Text(refa.SKU ?? refa.Producto?.SKU ?? "—");
                                tbl.Cell().PaddingVertical(2).Text(refa.NombreProducto);
                                tbl.Cell().PaddingVertical(2).AlignRight().Text($"{refa.Cantidad:0.##}");
                                tbl.Cell().PaddingVertical(2).AlignRight().Text($"${refa.PrecioUnitario:N2}");
                                tbl.Cell().PaddingVertical(2).AlignRight().Text($"${refa.Importe:N2}");
                            }
                        });
                        t.Item().PaddingTop(4).AlignRight().Text(
                            $"Mano de obra: ${os.CostoManoObra:N2}   |   Refacciones: ${os.SubtotalRefacciones:N2}   |   TOTAL: ${os.Total:N2}").Bold();
                    }));
                }

                if (!string.IsNullOrWhiteSpace(os.ObservacionesTecnicas) || !string.IsNullOrWhiteSpace(os.ObservacionesRecepcion))
                {
                    col.Item().Element(e => Section(e, "OBSERVACIONES Y RECOMENDACIONES", t =>
                    {
                        if (!string.IsNullOrWhiteSpace(os.ObservacionesRecepcion))
                            t.Item().Text($"Recepción: {os.ObservacionesRecepcion}");
                        if (!string.IsNullOrWhiteSpace(os.ObservacionesTecnicas))
                            t.Item().Text($"Técnicas: {os.ObservacionesTecnicas}");
                    }));
                }

                var fotosConBytes = os.Fotografias
                    .Select(f => new { Foto = f, Bytes = ReadIf(f.ImagenUrl) })
                    .Where(x => x.Bytes != null)
                    .Take(6)
                    .ToList();

                if (fotosConBytes.Count > 0)
                {
                    col.Item().Element(e => Section(e, "EVIDENCIA FOTOGRÁFICA", t =>
                    {
                        t.Item().Row(r =>
                        {
                            foreach (var item in fotosConBytes.Take(3))
                            {
                                r.RelativeItem().Padding(3).Column(fc =>
                                {
                                    fc.Item().Height(85).AlignCenter().Image(item.Bytes!).FitArea();
                                    fc.Item().AlignCenter().Text(item.Foto.Categoria).Bold().FontSize(7.5f);
                                    if (!string.IsNullOrWhiteSpace(item.Foto.Descripcion))
                                        fc.Item().AlignCenter().Text(item.Foto.Descripcion).FontSize(7f).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });
                    }));
                }

                col.Item().PaddingTop(10).Row(r =>
                {
                    r.RelativeItem().Element(e => Firma(e, firmaTec, os.Tecnico?.Nombre ?? "Técnico Asignado", "Firma Técnico Responsable"));
                    r.ConstantItem(14);
                    r.RelativeItem().Element(e => Firma(e, firmaSup, os.Supervisor?.Nombre ?? _o.ResponsableNombre, "Firma Supervisor"));
                    r.ConstantItem(14);
                    r.RelativeItem().Element(e => Firma(e, firmaCli, os.NombreClienteFirma ?? cl.Contacto ?? cl.RazonSocial, "Firma de Conformidad Cliente"));
                    r.ConstantItem(14);
                    r.ConstantItem(68).Column(q =>
                    {
                        q.Item().Image(qrPng).FitWidth();
                        q.Item().AlignCenter().Text(os.Folio).FontSize(6.5f);
                    });
                });
            });

            page.Footer().Column(f =>
            {
                f.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Medium);
                f.Item().PaddingTop(3).Row(r =>
                {
                    r.RelativeItem().Text("GEOLABORATORIO ERP · Centro de Servicio, Calibración e Ingeniería Topográfica").FontSize(7);
                    r.ConstantItem(80).AlignRight().Text(x => { x.Span("Pág. ").FontSize(7); x.CurrentPageNumber().FontSize(7); x.Span("/").FontSize(7); x.TotalPages().FontSize(7); });
                });
            });
        })).GeneratePdf();
    }

    private static void Section(IContainer e, string title, Action<ColumnDescriptor> body)
        => e.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Column(c =>
        {
            c.Item().Background(Light).Padding(4).PaddingLeft(8).Text(title).Bold().FontSize(8.5f).FontColor(Blue);
            c.Item().Padding(6).Column(body);
        });

    private static void Row(ColumnDescriptor c, string label, string value)
        => c.Item().PaddingBottom(2).Row(r =>
        {
            r.ConstantItem(110).Text(label).SemiBold().FontColor(Colors.Grey.Darken2);
            r.RelativeItem().Text(value);
        });

    private static void Firma(IContainer e, byte[]? img, string nombre, string puesto)
        => e.Column(c =>
        {
            var slot = c.Item().Height(42).AlignCenter();
            if (img != null) slot.Image(img).FitArea(); else slot.Text("");
            c.Item().LineHorizontal(0.75f);
            c.Item().AlignCenter().Text(nombre).SemiBold().FontSize(8.5f);
            c.Item().AlignCenter().Text(puesto).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
        });
}
