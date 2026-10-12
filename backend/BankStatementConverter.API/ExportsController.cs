using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;
[Route("api/export-templates")]
public class ExportsController(CatalogService<ExportTemplate, ExportInput, ExportDto> service) : CatalogController<ExportTemplate, ExportInput, ExportDto>(service);
