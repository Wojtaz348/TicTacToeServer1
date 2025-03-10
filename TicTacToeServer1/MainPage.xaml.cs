using Microsoft.Maui.Controls;
using System;

namespace TicTacToeServer1
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            // Rozwiązanie 1: Ręczne tworzenie interfejsu
            Content = CreateInterface();
        }

        private View CreateInterface()
        {
            // Utworzenie przycisku programowo
            CounterBtn = new Button
            {
                Text = "Click me",
                HorizontalOptions = LayoutOptions.Center
            };
            CounterBtn.Clicked += OnCounterClicked;

            // Utworzenie głównego układu
            return new VerticalStackLayout
            {
                Spacing = 25,
                Padding = new Thickness(30, 0),
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = "Tic Tac Toe Server",
                        FontSize = 32,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    CounterBtn
                }
            };
        }

        // Deklaracja przycisku jako pole klasy
        private Button CounterBtn;

        private void OnCounterClicked(object sender, EventArgs e)
        {
            count++;
            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";
            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}