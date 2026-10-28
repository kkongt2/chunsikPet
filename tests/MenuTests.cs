using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ChunsikDesktop;
internal static class MenuTests {
    static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL menu: " + name); }
    static MenuItem Find(ContextMenu menu, string prefix) { foreach (object value in menu.Items) { MenuItem item = value as MenuItem; if (item != null && item.Header is string && ((string)item.Header).StartsWith(prefix)) return item; } return null; }
    static void Pump() { Application.Current.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate {})); }
    public static void Run() {
        List<PetWindow> pool = (List<PetWindow>)typeof(PetWindow).GetField("Pets", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        PetWindow placeholder = new PetWindow(true); pool.Add(placeholder);
        PetWindow pet = new PetWindow();
        try {
            pet.Show(); Pump();
            ((DispatcherTimer)typeof(PetWindow).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pet)).Stop();
            ContextMenu menu = pet.ContextMenu; menu.IsOpen = true; Pump();
            MenuItem speed = Find(menu, "걷는 속도");
            Check(((MenuItem)speed.Items[1]).IsChecked, "current default speed checked");
            ((MenuItem)speed.Items[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.IsOpen = false; menu.IsOpen = true; Pump();
            Check(((MenuItem)speed.Items[2]).IsChecked && speed.Header.ToString().Contains("빠르게"), "speed command reflected after reopen");
            Check(!PetWindow.CommonSpeed.HasValue, "mixed pet speed is not presented as one selected value");
            PetWindow.SpeedAll(22); Check(PetWindow.CommonSpeed == 22, "common speed restored after all command");
            Check(Find(menu, "테스트 알림") == null && Find(menu, "알림 설정") == null && Find(menu, "모두 종료") == null, "global and diagnostic actions removed from pet menu");
            Check(Find(menu, "이 춘식이 제거") != null, "multiple pets offer individual removal");
            menu.IsOpen = false; pool.Remove(placeholder); menu.IsOpen = true; Pump();
            Check(Find(menu, "프로그램 종료…") != null && Find(menu, "이 춘식이 제거") == null, "last pet clearly offers application shutdown");
            StackPanel quick = (StackPanel)((MenuItem)menu.Items[0]).Header;
            var buttons = (System.Windows.Controls.Primitives.UniformGrid)quick.Children[1];
            Button rest = (Button)buttons.Children[0]; rest.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((TextBlock)((StackPanel)rest.Content).Children[1]).Text == "다시 걷기", "quick action names next action after switching mode");
        } finally { pet.ContextMenu.IsOpen = false; if (!pool.Contains(placeholder)) pool.Add(placeholder); pet.Close(); pool.Remove(placeholder); placeholder.Close(); }
        Check(TimeToolWindow.TimerMenuLabel == "타이머…", "closed timer advertises opening action");
        Console.WriteLine("PASS: menu scopes, speed selection, mixed state and last-pet shutdown label");
    }
}
