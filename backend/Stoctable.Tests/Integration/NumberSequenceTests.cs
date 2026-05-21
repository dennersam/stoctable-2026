using Stoctable.Infrastructure.Repositories;

namespace Stoctable.Tests.Integration;

[Trait("Category", "Integration")]
public class NumberSequenceTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public NumberSequenceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NextAsync_SerialCalls_ReturnsIncreasingValues()
    {
        await using var ctx = _fixture.CreateContext();
        var gen = new NumberSequenceGenerator(ctx);

        var a = await gen.NextAsync("TST_SERIAL");
        var b = await gen.NextAsync("TST_SERIAL");
        var c = await gen.NextAsync("TST_SERIAL");

        Assert.Equal(1, a);
        Assert.Equal(2, b);
        Assert.Equal(3, c);
    }

    [Fact]
    public async Task NextAsync_ConcurrentCalls_AllValuesUnique()
    {
        const string prefix = "TST_CONCURRENT";
        const int taskCount = 20;

        // Cada task usa seu próprio DbContext (simula requests paralelos)
        var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () =>
        {
            await using var ctx = _fixture.CreateContext();
            var gen = new NumberSequenceGenerator(ctx);
            return await gen.NextAsync(prefix);
        })).ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(taskCount, results.Distinct().Count());
        Assert.Equal(taskCount, results.Max());
        Assert.Equal(1, results.Min());
    }

    [Fact]
    public async Task NextAsync_DifferentPrefixes_SequencesIndependent()
    {
        await using var ctx = _fixture.CreateContext();
        var gen = new NumberSequenceGenerator(ctx);

        Assert.Equal(1, await gen.NextAsync("PFX_A"));
        Assert.Equal(2, await gen.NextAsync("PFX_A"));
        Assert.Equal(1, await gen.NextAsync("PFX_B"));
        Assert.Equal(3, await gen.NextAsync("PFX_A"));
        Assert.Equal(2, await gen.NextAsync("PFX_B"));
    }
}
