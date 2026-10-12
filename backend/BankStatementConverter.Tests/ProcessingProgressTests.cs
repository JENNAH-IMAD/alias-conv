using BankStatementConverter.API;
using BankStatementConverter.Application;
using Xunit;

namespace BankStatementConverter.Tests;

public sealed class ProcessingProgressTests
{
    [Fact]
    public void ProgressIsPrivateMonotonicAndTerminal()
    {
        var store=new ProcessingProgressStore();
        var id=Guid.NewGuid();
        store.Start(id,"owner");
        Assert.Equal(404,Assert.Throws<AppException>(()=>store.Get(id,"another-user")).Status);
        Assert.Equal(409,Assert.Throws<AppException>(()=>store.Start(id,"owner")).Status);
        store.Report(id,"reading",60,3,5);
        store.Report(id,"reading",40,3,5);
        Assert.Equal(60,store.Get(id,"owner").Percent);
        Assert.Equal(3,store.Get(id,"owner").ExtractedPages);
        store.Report(id,"done",100,5,5);
        store.Report(id,"reading",20,0,5);
        Assert.Equal("done",store.Get(id,"owner").Stage);
        Assert.Equal(100,store.Get(id,"owner").Percent);
    }
    [Fact]
    public void FailureRetainsLastCompletedProgressAndCannotBecomeSuccessful()
    {
        var store=new ProcessingProgressStore();
        var scope=new RequestProcessingProgress(store);
        var id=Guid.NewGuid();
        scope.Start(id,"owner"); scope.Report("reading",40,1,3); scope.Report("failed",0);
        scope.Report("done",100);
        Assert.Equal("failed",store.Get(id,"owner").Stage);
        Assert.Equal(40,store.Get(id,"owner").Percent);
        Assert.Equal("reading",store.Get(id,"owner").ActiveStage);
    }
    [Fact]
    public void ExpiredJobsDisappearAndCapacityIsBounded()
    {
        var clock=new TestClock();
        var store=new ProcessingProgressStore(clock);
        var id=Guid.NewGuid(); store.Start(id,"owner");
        clock.Now=clock.Now.AddMinutes(31);
        Assert.Equal(404,Assert.Throws<AppException>(()=>store.Get(id,"owner")).Status);
        store.Start(id,"owner"); // Start also clears expired entries.
        for(var n=1;n<512;n++) store.Start(Guid.NewGuid(),"owner");
        Assert.Equal(429,Assert.Throws<AppException>(()=>store.Start(Guid.NewGuid(),"owner")).Status);
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    [Fact]
    public void CompletedHistoryDoesNotBlockNewConversionsOrEvictActiveUsers()
    {
        var store=new ProcessingProgressStore();var active=Guid.NewGuid();store.Start(active,"active-user");
        for(var n=0;n<511;n++) {var id=Guid.NewGuid();store.Start(id,"previous-user");store.Report(id,"done",100,null,null);}
        var next=Guid.NewGuid();store.Start(next,"new-user");
        Assert.Equal("checking",store.Get(next,"new-user").Stage);
        Assert.Equal("checking",store.Get(active,"active-user").Stage);
        Assert.Equal(404,Assert.Throws<AppException>(()=>store.Get(next,"previous-user")).Status);
    }
}
