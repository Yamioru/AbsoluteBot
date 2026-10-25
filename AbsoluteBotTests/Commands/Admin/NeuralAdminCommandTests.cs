using AbsoluteBot.Chat.Commands.AdminCommands;
using AbsoluteBot.Models;
using AbsoluteBot.Services.NeuralNetworkServices;
using AbsoluteBotTests.Infrastructure;

namespace AbsoluteBotTests.Commands.Admin;

[Collection("BeginData")]
[Trait("Category", "Unit")]
[Trait("Area", "Commands")]
public class NeuralAdminCommandTests(BeginDataFixture fixture)
{
    [Fact]
    public void ApiMode_CanExecute_AdminChannelAndAdministrator()
    {
        var cmd = new ApiModeCommand(new NeuralModelConfigService());
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, role: UserRole.Administrator)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, role: UserRole.Moderator)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator)));
    }

    [Fact]
    public async Task ApiMode_SwitchesFreeAndPaid()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        var cmd = new ApiModeCommand(models);

        var paid = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "paid"));
        Assert.Contains("платный", paid);
        Assert.False(models.IsFreeTier());

        var free = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "free"));
        Assert.Contains("бесплатный", free);
        Assert.True(models.IsFreeTier());
    }

    [Fact]
    public void SetModel_CanExecute_AdminChannelAndAdministrator()
    {
        var cmd = new SetModelCommand(new NeuralModelConfigService());
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator)));
    }

    [Fact]
    public async Task SetModel_Dash_ClearsFallback()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        var cmd = new SetModelCommand(models);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(
            cmd.Name, "Ask gemini gemini-3.1-flash-lite -"));

        Assert.Contains("замена не задана", result);
        Assert.Single(models.GetGeminiAttemptModels(NeuralEntities.Ask));

        await models.SetModelAsync(NeuralEntities.Ask, "gemini-3.8-flash", NeuralProviders.Gemini, "gemini-3.5-flash-lite", true);
    }

    [Fact]
    public async Task SetModel_HappyPath_CurrentProvider()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        await models.SetProviderAsync(NeuralProviders.Gemini);
        var cmd = new SetModelCommand(models);

        var result = await cmd.ExecuteAsync(
            ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "Ask gemini-3.1-flash-lite"));

        Assert.Contains("Ask", result);
        Assert.Equal("gemini-3.1-flash-lite", models.GetModel(NeuralEntities.Ask));
        await models.SetModelAsync(NeuralEntities.Ask, "gemini-3.8-flash", NeuralProviders.Gemini, "gemini-3.5-flash-lite", true);
    }

    [Fact]
    public async Task SetModel_HappyPath_ExplicitProvider()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        var cmd = new SetModelCommand(models);

        var result = await cmd.ExecuteAsync(
            ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "Chat groq openai/gpt-oss-120b"));

        Assert.Contains("groq", result);
        await models.SetProviderAsync(NeuralProviders.Groq);
        Assert.Equal("openai/gpt-oss-120b", models.GetModel(NeuralEntities.Chat));
        await models.SetProviderAsync(NeuralProviders.Gemini);
    }

    [Fact]
    public async Task SetModel_HappyPath_WithFallback()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        var cmd = new SetModelCommand(models);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(
            cmd.Name, "GoogleSearch gemini gemini-3.5-flash gemini-3.1-flash-lite"));

        Assert.Contains("замена gemini-3.1-flash-lite", result);
        Assert.Equal(
            new[] {"gemini-3.5-flash", "gemini-3.1-flash-lite"},
            models.GetGeminiAttemptModels(NeuralEntities.GoogleSearch));
        await models.SetModelAsync(
            NeuralEntities.GoogleSearch, "gemini-3.8-flash", NeuralProviders.Gemini, "gemini-3.5-flash-lite", true);
    }

    [Fact]
    public async Task SetModel_MissingParameters_ReturnsUsage()
    {
        var cmd = new SetModelCommand(new NeuralModelConfigService());
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "Chat"));
        Assert.Contains("Использование", result);
    }

    [Fact]
    public void ShowModels_CanExecute_AdminChannelAndAdministrator()
    {
        var cmd = new ShowModelsCommand(new NeuralModelConfigService());
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator)));
    }

    [Fact]
    public async Task ShowModels_HappyPath()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        var cmd = new ShowModelsCommand(models);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name));

        Assert.Contains("Провайдер:", result);
        Assert.Contains("бесплатный", result);
        Assert.Contains("Chat:", result);
        Assert.Contains("Cloudflare", result);
        Assert.Contains("запасной", result);
    }

    [Fact]
    public void SwitchAi_CanExecute_AdminChannelAndAdministrator()
    {
        var cmd = new SwitchAiCommand(new NeuralModelConfigService());
        Assert.True(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, role: UserRole.Administrator)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, role: UserRole.Moderator)));
        Assert.False(cmd.CanExecute(ChatContextFactory.CreateParsed(cmd.Name, role: UserRole.Administrator)));
    }

    [Fact]
    public async Task SwitchAi_HappyPath()
    {
        _ = fixture;
        var models = new NeuralModelConfigService();
        await models.InitializeAsync();
        var cmd = new SwitchAiCommand(models);

        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name, "groq"));

        Assert.Contains("groq", result);
        Assert.Equal(NeuralProviders.Groq, models.GetProvider());

        await models.SetProviderAsync(NeuralProviders.Gemini);
    }

    [Fact]
    public async Task SwitchAi_MissingParameters_ReturnsUsage()
    {
        var cmd = new SwitchAiCommand(new NeuralModelConfigService());
        var result = await cmd.ExecuteAsync(ChatContextFactory.CreateAdminChannelParsed(cmd.Name));
        Assert.Contains("Использование", result);
    }
}