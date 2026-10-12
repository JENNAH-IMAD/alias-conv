using BankStatementConverter.Application;
using BankStatementConverter.Domain;
using BankStatementConverter.Infrastructure;
using Microsoft.AspNetCore.Mvc;
namespace BankStatementConverter.API;

[Route("api/bank-accounts")] public class AccountsController(CatalogService<BankAccount, BankAccountInput, BankAccountDto> s) : CatalogController<BankAccount, BankAccountInput, BankAccountDto>(s);
