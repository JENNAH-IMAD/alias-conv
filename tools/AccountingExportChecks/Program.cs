if (args.Contains("--archive-cleanup-only")) { await ArchiveCleanupChecks.Run(); return; }
await ArchiveCleanupChecks.Run();
EntryModelChecks.Run();
Sage100TemplateChecks.Run();
Console.WriteLine("Accounting checks passed: archive cleanup, entry classification, tax calculation and export template compatibility.");
