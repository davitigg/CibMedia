using CibMedia.Core.Abstractions;
using CibMedia.Core.Presentation.Setup;
using CibMedia.Core.Tests.Fakes;
using Xunit;

namespace CibMedia.Core.Tests.Presentation;

public sealed class ApiKeysSetupViewModelTests
{
    private const string TmdbShape = "0123456789abcdef0123456789abcdef";

    // Only the issuing service gets an opinion about what a key looks like.
    [Theory]
    [InlineData("not-a-key-at-all")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("{3f2b9c1a-6d4e-4a91-b0c7-5e28d1f6a340}")]
    public async Task Asks_the_service_about_anything_that_was_typed(string key)
    {
        var (vm, keys, check, _) = Build();
        using var model = vm;

        await model.SubmitAsync(key);

        Assert.Equal([(ApiKeyKind.Tmdb, key)], check.Asked);
        Assert.Equal(key, keys[ApiKeyKind.Tmdb]);
        Assert.True(model.State.Complete);
    }

    [Fact]
    public async Task Is_not_finished_until_a_key_has_been_typed()
    {
        var (vm, keys, check, _) = Build();
        using var model = vm;

        await model.SubmitAsync(null);

        Assert.Null(keys[ApiKeyKind.Tmdb]);
        Assert.Empty(check.Asked);
        Assert.Equal(ApiKeySetupStatus.Waiting, model.State.Tmdb);
        Assert.False(model.State.Complete);
    }

    [Fact]
    public async Task Does_not_re_check_a_key_that_was_left_alone()
    {
        var (vm, keys, check, _) = Build();
        using var model = vm;
        keys.Save(ApiKeyKind.Tmdb, TmdbShape);

        await model.SubmitAsync(TmdbShape);

        Assert.Empty(check.Asked);
        Assert.True(model.State.Complete);
    }

    [Fact]
    public async Task Does_not_store_a_key_the_service_turns_down()
    {
        var (vm, keys, check, _) = Build();
        using var model = vm;
        check.Accepts = false;

        await model.SubmitAsync(TmdbShape);

        Assert.Null(keys[ApiKeyKind.Tmdb]);
        Assert.Equal(ApiKeySetupStatus.Rejected, model.State.Tmdb);
    }

    [Fact]
    public async Task Separates_a_key_it_could_not_check_from_one_that_was_refused()
    {
        var (vm, keys, check, _) = Build();
        using var model = vm;
        check.Throws = new HttpRequestException("no route to host");

        await model.SubmitAsync(TmdbShape);

        Assert.Null(keys[ApiKeyKind.Tmdb]);
        Assert.Equal(ApiKeySetupStatus.Unreachable, model.State.Tmdb);
    }

    [Fact]
    public async Task Trims_what_a_clipboard_carries_with_a_key()
    {
        var (vm, keys, _, _) = Build();
        using var model = vm;

        await model.SubmitAsync($"  {TmdbShape}\n");

        Assert.Equal(TmdbShape, keys[ApiKeyKind.Tmdb]);
    }

    [Fact]
    public async Task Takes_a_key_posted_to_the_page_the_same_way_as_a_typed_one()
    {
        var (vm, keys, _, server) = Build();
        using var model = vm;
        var saved = new TaskCompletionSource();

        model.StateChanged += () =>
        {
            if (model.State.Complete) saved.TrySetResult();
        };

        await model.StartAsync();
        await server.PostAsync(SetupController.Path, $"tmdb={TmdbShape}");

        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(TmdbShape, keys[ApiKeyKind.Tmdb]);
    }

    [Fact]
    public async Task Turns_down_a_post_that_carries_no_key_at_all()
    {
        var (vm, _, _, server) = Build();
        using var model = vm;

        await model.StartAsync();
        var response = await server.PostAsync(SetupController.Path, "tmdb=");

        Assert.Equal(400, response.Status);
    }

    // A good key closes the screen serving the page, so the reply cannot wait on the verdict.
    [Fact]
    public async Task Answers_the_phone_before_the_key_is_checked()
    {
        var (vm, _, check, server) = Build();
        using var model = vm;
        var blocked = new TaskCompletionSource();
        check.Gate = blocked.Task;

        await model.StartAsync();
        var response = await server.PostAsync(SetupController.Path, $"tmdb={TmdbShape}");

        Assert.Equal(202, response.Status);

        blocked.SetResult();
    }

    [Fact]
    public async Task Serves_the_form_to_a_phone_that_has_only_opened_the_page()
    {
        var (vm, _, _, server) = Build();
        using var model = vm;

        await model.StartAsync();
        var response = await server.GetAsync(SetupController.Path);

        Assert.Equal(200, response.Status);
        Assert.Contains("Paste your TMDb key", response.Body);
    }

    [Fact]
    public async Task Publishes_the_address_a_phone_should_send_to()
    {
        var (vm, _, _, server) = Build();
        using var model = vm;

        Assert.Null(model.State.Address);

        await model.StartAsync();

        Assert.Equal(server.Address + SetupController.Path, model.State.Address);
    }

    [Fact]
    public async Task Releases_its_route_when_the_screen_goes()
    {
        var (vm, _, _, server) = Build();
        await vm.StartAsync();

        vm.Dispose();

        Assert.Equal(1, server.Released);
    }

    [Fact]
    public async Task Gives_the_port_back_when_the_screen_leaves_the_foreground()
    {
        var (vm, _, _, server) = Build();
        using var model = vm;

        await model.StartAsync();
        Assert.NotNull(model.State.Address);

        await model.StopAsync();

        Assert.Equal(1, server.Released);
        Assert.Null(model.State.Address);
    }

    [Fact]
    public async Task Serves_again_after_being_stopped()
    {
        var (vm, _, _, server) = Build();
        using var model = vm;

        await model.StartAsync();
        await model.StopAsync();
        await model.StartAsync();

        Assert.Equal(2, server.Served);
        Assert.Equal(server.Address + SetupController.Path, model.State.Address);
    }

    private static (ApiKeysSetupViewModel, FakeApiKeys, FakeApiKeyCheck, FakeLocalHttpServer) Build()
    {
        var keys = new FakeApiKeys();
        var check = new FakeApiKeyCheck();
        var server = new FakeLocalHttpServer();

        return (new ApiKeysSetupViewModel(keys, check, server), keys, check, server);
    }
}
