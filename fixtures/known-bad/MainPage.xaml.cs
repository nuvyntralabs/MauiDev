public partial class MainPage
{
    public MainPage()
    {
        Clicked += OnClicked;
        _ = Task.Run(() => new HttpClient());
    }

    void OnClicked(object? sender, EventArgs e)
    {
    }
}
