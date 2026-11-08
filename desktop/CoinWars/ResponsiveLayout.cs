using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CoinWars;
public partial class GameWindow
{
    bool CompactLayout;
    bool WantsCompactLayout => (ActualWidth>0?ActualWidth:Width)<1200 || (ActualHeight>0?ActualHeight:Height)<760;
    void ConfigureLayout()
    {
        // Keep battle coordinates stable until the current round has finished.
        if(!ShowingBattle&&!animating)CompactLayout=WantsCompactLayout;
        surface.Width=scene.Width=CompactLayout?1040:1440;
        surface.Height=scene.Height=CompactLayout?1780:900;
        overlays.Width=CompactLayout?1040:1440;overlays.Height=CompactLayout?760:900;
        viewport.VerticalScrollBarVisibility=CompactLayout?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        effects.HorizontalAlignment=HorizontalAlignment.Left;effects.VerticalAlignment=VerticalAlignment.Top;
        effects.RenderTransform=new TranslateTransform(CompactLayout?-220:0,0);
    }
    void Section(Action draw,double x,double y,double scale=1)
    {
        int start=scene.Children.Count;draw();
        var children=scene.Children.Cast<UIElement>().Skip(start).ToArray();
        var group=new Canvas {Width=1440,Height=900,RenderTransform=new MatrixTransform(scale,0,0,scale,x,y)};
        foreach(var child in children){scene.Children.Remove(child);group.Children.Add(child);}
        scene.Children.Add(group);
    }
    void RenderCompactLayout()
    {
        Section(()=>{BackgroundScene();Stage();},-220,0);
        Section(Header,0,0,1040.0/1440);
        Section(Bench,0,168,1040.0/1440);
        Put(scene,Btn("編成・装備を見る ↓",()=>viewport.ScrollToVerticalOffset(1070*ActualScale())),24,681,300,42);
        Put(scene,Label("小画面表示：ホイールで上下に移動",15,Muted),345,681,670,42);
        Shop();
        Section(Synergies,-8,930,1.35);
        Section(Sidebar,-1205,930,1.35);
        Put(scene,Btn("ショップへ戻る ↑",()=>viewport.ScrollToVerticalOffset(700*ActualScale())),760,1100,250,48);
        Put(scene,Label(message,16,White),24,1710,990,60);
    }
    double ActualScale()=>Math.Min(1,Math.Max(1,viewport.ViewportWidth)/1040.0);
}
