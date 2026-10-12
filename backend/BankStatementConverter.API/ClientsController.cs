using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

[Route("api/clients")] public class ClientsController(CatalogService<Client, ClientInput, ClientDto> s) : CatalogController<Client, ClientInput, ClientDto>(s);
