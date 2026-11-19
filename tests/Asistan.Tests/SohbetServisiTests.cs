using Asistan.Application.Sohbet;
using FluentAssertions;
using Moq;

namespace Asistan.Tests;

public class SohbetServisiTests
{
    private readonly Mock<ILlmIstemcisi> _llm = new();
    private readonly SohbetServisi _servis;

    public SohbetServisiTests() => _servis = new SohbetServisi(_llm.Object);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SorAsync_BosSoru_HataFirlatirVeLlmCagrilmaz(string soru)
    {
        var islem = () => _servis.SorAsync("kullanici-1", soru);

        await islem.Should().ThrowAsync<ArgumentException>();
        _llm.Verify(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SorAsync_GecerliSoru_ModelinCevabiniDoner()
    {
        _llm.Setup(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Merhaba!");

        var cevap = await _servis.SorAsync("kullanici-1", "Selam");

        cevap.Should().Be("Merhaba!");
    }

    [Fact]
    public async Task SorAsync_GecerliSoru_SistemPromptuVeSoruyuSirayladaGonderir()
    {
        List<LlmMesaj>? gonderilen = null;
        _llm.Setup(x => x.TamamlaAsync(It.IsAny<IEnumerable<LlmMesaj>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<LlmMesaj>, string, CancellationToken>((m, _, _) => gonderilen = m.ToList())
            .ReturnsAsync("ok");

        await _servis.SorAsync("kullanici-1", "  DI nedir?  ");

        gonderilen.Should().NotBeNull();
        gonderilen!.Should().HaveCount(2);
        gonderilen[0].Should().Be(new LlmMesaj("system", SohbetServisi.SistemPrompt));
        gonderilen[1].Should().Be(new LlmMesaj("user", "DI nedir?"));
    }
}
