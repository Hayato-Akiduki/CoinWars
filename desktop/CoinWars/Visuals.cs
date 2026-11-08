using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Shapes;

namespace CoinWars;

public partial class GameWindow
{
    static readonly Brush White = B("#F1F1EF"), Muted = B("#AFBAD4"), Gold = B("#E9D5A1"), Cyan = B("#89F0EF");
    static readonly string[] CostColors = ["", "#657A9C", "#3C9A89", "#4D83BA", "#8F6CBD", "#BE9860"];
    static readonly Dictionary<string, ImageSource> sprites = [];
    static readonly Dictionary<string, ImageSource> portraitOverrides = [];
    static readonly ControlTemplate buttonTemplate = (ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
          <Border x:Name="Frame" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="2" Padding="{TemplateBinding Padding}">
            <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Frame" Property="BorderBrush" Value="#C9FFFF"/></Trigger>
            <Trigger Property="IsPressed" Value="True"><Setter Property="Opacity" Value="0.7"/></Trigger>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """);
    static Color ColorOf(string value) => (Color)ColorConverter.ConvertFromString(value);
    static Brush B(string value) { var b = new SolidColorBrush(ColorOf(value)); b.Freeze(); return b; }
    static double textScale = 1;
    static TextBlock Label(string value, double size, Brush color, bool bold = false) => new()
    {
        Text = value, FontSize = size * textScale, Tag = size,
        ToolTip = new TextBlock { Text=value,FontSize=16*textScale,MaxWidth=600,TextWrapping=TextWrapping.Wrap },
        Foreground = color, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    static void Put(Canvas canvas, FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x); Canvas.SetTop(element, y); element.Width = width; element.Height = height; canvas.Children.Add(element);
    }
    static Border Plate(string fill, string stroke) => new()
    {
        Background = B(fill), BorderBrush = B(stroke), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2)
    };
    Button Btn(string text, Action action, bool enabled = true, bool primary = false)
    {
        var b = new Button
        {
            Content = Label(text, 14, primary ? B("#14213A") : White, primary), IsEnabled = enabled,
            Background = primary ? B("#CBE7EA") : B("#E3222C42"), BorderBrush = primary ? Cyan : B("#596680"),
            BorderThickness = new Thickness(1), Padding = new Thickness(8, 3, 8, 3), Template = buttonTemplate,
            HorizontalContentAlignment = HorizontalAlignment.Center, Cursor = Cursors.Hand
        };
        b.Click += (_, _) => action(); return b;
    }
    static FrameworkElement Meter(double value, string color)
    {
        var grid = new Grid { Background = B("#263047"), ClipToBounds = true };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Clamp(value,0,1), GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0,1-Math.Clamp(value,0,1)), GridUnitType.Star) });
        grid.Children.Add(new Border { Background = B(color) }); return grid;
    }
    static void Line(Canvas canvas, double x1, double y1, double x2, double y2, string color, double thickness = 1)
        => canvas.Children.Add(new Line { X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=B(color),StrokeThickness=thickness,IsHitTestVisible=false });
    static void Polygon(Canvas canvas, string points, Brush fill, string stroke = "#4659A7")
    {
        canvas.Children.Add(new Polygon { Points=PointCollection.Parse(points),Fill=fill,Stroke=B(stroke),StrokeThickness=1,IsHitTestVisible=false });
    }
    void BackgroundScene()
    {
        scene.Background = B("#101521");
        Put(scene, new Border { Background = new LinearGradientBrush(ColorOf("#202A3C"), ColorOf("#172030"),90), CornerRadius = new CornerRadius(8) }, 244,102,940,532);
        // One quiet divider keeps front and rear rows readable without a decorative floor.
        Line(scene,276,376,1152,376,"#354052",1);
    }
    static string TraitSymbol(string key) => key switch { "knight"=>"⚔","guard"=>"◈","mage"=>"✦","wind"=>"≋",_=>"◉" };
    static string ItemSymbol(string key) => key switch { "blade"=>"⚔","armor"=>"⬟","boots"=>"»","tome"=>"▤","fang"=>"♧","amulet"=>"◈","crystal"=>"✦",_=>"◉" };
    static ImageSource? Sprite(string key)
    {
        // Individual portraits take precedence without replacing the other atlas cells.
        if (portraitOverrides.TryGetValue(key,out var portrait)) return portrait;
        string overridePath=System.IO.Path.Combine(AppContext.BaseDirectory,"Assets",key+".png");
        if (File.Exists(overridePath))
        {
            var image=new BitmapImage();
            image.BeginInit();image.UriSource=new Uri(overridePath);image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();
            portraitOverrides[key]=image;
            return image;
        }
        if (sprites.TryGetValue(key,out var cached)) return cached;
        string atlas=System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","characters.png");
        if (File.Exists(atlas))
        {
            var bitmap=new BitmapImage();
            bitmap.BeginInit();bitmap.UriSource=new Uri(atlas);bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();
            for (int index=0;index<Data.Units.Length;index++)
            {
                int left=index%4*bitmap.PixelWidth/4,top=index/4*bitmap.PixelHeight/4;
                int right=(index%4+1)*bitmap.PixelWidth/4,bottom=(index/4+1)*bitmap.PixelHeight/4;
                var image=new CroppedBitmap(bitmap,new Int32Rect(left,top,right-left,bottom-top));image.Freeze();sprites[Data.Units[index].Key]=image;
            }
            return sprites.GetValueOrDefault(key);
        }
        string file=System.IO.Path.Combine(AppContext.BaseDirectory,"img",key+".png");
        if (!File.Exists(file)) return null;
        var fallback=new BitmapImage(new Uri(file));fallback.Freeze();sprites[key]=fallback;return fallback;
    }
    enum PortraitFraming { Face, UpperBody }
    static FrameworkElement Portrait(string key, PortraitFraming framing = PortraitFraming.Face)
    {
        var source=Sprite(key);
        if (source==null) return Label(TraitSymbol(Data.Def(key).Traits[0]),32,Gold);
        // The atlas contains purpose-drawn busts; do not apply the old full-body zoom.
        bool face = framing == PortraitFraming.Face;
        var crop = face ? new Rect(.14,.08,.76,.76) : new Rect(0,0,1,1);
        var portrait = new Border { ClipToBounds=true,Background=new ImageBrush(source) {
            Stretch=Stretch.UniformToFill,Viewbox=crop,ViewboxUnits=BrushMappingMode.RelativeToBoundingBox,
            AlignmentX=AlignmentX.Center,AlignmentY=face?AlignmentY.Center:AlignmentY.Top} };
        RenderOptions.SetBitmapScalingMode(portrait, BitmapScalingMode.HighQuality);
        return portrait;
    }
    FrameworkElement Actor(Unit? unit, Fighter? fighter, bool bench, int index, bool enemy)
    {
        var grid=new Grid {Background=Brushes.Transparent,ClipToBounds=false,RenderTransformOrigin=new Point(.5,.8),RenderTransform=new TranslateTransform()};
        bool selectedHere=!enemy&&selected is {} x&&x.bench==bench&&x.index==index;
        var frame = new Border {BorderBrush=selectedHere?Gold:B("#354052"),
            BorderThickness=new Thickness(selectedHere?2:unit==null||bench?1:0),Background=B(selectedHere?"#503F5287":bench?"#B7162038":"#00172030"),
            Margin=bench?new Thickness(0):new Thickness(15,36,15,18)};
        grid.Children.Add(frame);
        if (unit==null)
        {
            grid.Children.Add(new TextBlock {Text="◇",FontSize=bench?22:40,Foreground=B("#6377AC"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
        }
        else
        {
            var d=Data.Def(unit);
            var portrait=Portrait(unit.Key,bench?PortraitFraming.Face:PortraitFraming.UpperBody);
            portrait.Margin=bench?new Thickness(2,2,2,18):new Thickness(2,0,2,30);
            grid.Children.Add(portrait);
            var stars=Label(new string('★',unit.Star),bench?10:12,Gold,true);stars.VerticalAlignment=VerticalAlignment.Top;stars.Margin=new Thickness(6,0,0,0);
            grid.Children.Add(stars);
            var caption=new StackPanel {VerticalAlignment=VerticalAlignment.Bottom,Background=B("#D70F172B"),Margin=new Thickness(0,0,0,0)};
            var name=Label(d.Name,bench?11:13,White,true);name.TextWrapping=TextWrapping.NoWrap;name.TextAlignment=TextAlignment.Center;caption.Children.Add(name);
            if (fighter!=null)
            {
                var hp=Meter(fighter.Hp/fighter.MaxHp,fighter.Player?"#83DCE3":"#F08EAB");hp.Height=6;hp.Margin=new Thickness(3,3,3,1);caption.Children.Add(hp);
                var ep=Meter((double)fighter.Energy/fighter.Need,"#E9D5A1");ep.Height=3;ep.Margin=new Thickness(3,1,3,2);caption.Children.Add(ep);
                if (!fighter.Alive) grid.Opacity=.23;
            }
            else if (!bench)
            {
                var traits=Label(string.Join("・",d.Traits.Select(t=>Data.Traits[t])),9,Muted);traits.TextAlignment=TextAlignment.Center;caption.Children.Add(traits);
                if(unit.Items.Count>0){var equipment=Label(string.Join(" ",unit.Items.Select(ItemSymbol)),11,Gold);equipment.TextAlignment=TextAlignment.Center;caption.Children.Add(equipment);}
            }
            grid.Children.Add(caption);
            grid.ToolTip=$"{d.Name} {new string('★',unit.Star)}\nHP {d.Hp*Data.Mult(unit.Star):0} / 攻撃 {d.Atk*Data.Mult(unit.Star):0}\n{Data.Skills[d.Skill]}";
        }
        if (!enemy&&Planning)
        {
            var restingBorder=frame.BorderBrush;var restingThickness=frame.BorderThickness;
            grid.MouseEnter+=(_,_)=>{frame.BorderBrush=Cyan;frame.BorderThickness=new Thickness(2);};
            grid.MouseLeave+=(_,_)=>{frame.BorderBrush=restingBorder;frame.BorderThickness=restingThickness;};
            grid.Cursor=Cursors.Hand;grid.AllowDrop=true;
            grid.MouseLeftButtonDown+=(_,e)=>{dragStart=e.GetPosition(grid);grid.CaptureMouse();e.Handled=true;};
            grid.MouseMove+=(_,e)=>
            {
                if(e.LeftButton!=MouseButtonState.Pressed||unit==null||dragging)return;
                Point p=e.GetPosition(grid);
                if(Math.Abs(p.X-dragStart.X)<8&&Math.Abs(p.Y-dragStart.Y)<8)return;
                dragging=true;grid.ReleaseMouseCapture();
                try { DragDrop.DoDragDrop(grid,new DataObject("CoinWarsSlot",(bench,index)),DragDropEffects.Move); }
                finally {dragging=false;}
            };
            grid.MouseLeftButtonUp+=(_,e)=>{grid.ReleaseMouseCapture();if(!dragging)Slot(bench,index);e.Handled=true;};
            grid.DragOver+=(_,e)=>
            {
                bool equipment=e.Data.GetDataPresent("CoinWarsEquipment")&&unit!=null&&unit.Items.Count<3;
                bool character=e.Data.GetDataPresent("CoinWarsSlot");
                if(character&&e.Data.GetData("CoinWarsSlot") is ValueTuple<bool,int> source)
                    character=!(source.Item1&&!bench&&unit==null&&game.Count>=game.Capacity);
                e.Effects=equipment?DragDropEffects.Copy:character?DragDropEffects.Move:DragDropEffects.None;
                frame.BorderBrush=equipment||character?Cyan:B("#F19CAA");frame.BorderThickness=new Thickness(2);e.Handled=true;
            };
            grid.DragLeave+=(_,_)=>{frame.BorderBrush=restingBorder;frame.BorderThickness=restingThickness;};
            grid.Drop+=(_,e)=>
            {
                frame.BorderBrush=restingBorder;frame.BorderThickness=restingThickness;
                if(!Planning)return;
                if(e.Data.GetData("CoinWarsEquipment") is int equipment&&unit!=null)
                {
                    selected=null;selectedItem=null;Execute(()=>game.Equip(unit,equipment),"装備しました。","buy");e.Handled=true;
                }
                else if(e.Data.GetData("CoinWarsSlot") is ValueTuple<bool,int> from)
                {
                    selected=null;selectedItem=null;
                    Execute(()=>game.Move(from.Item1,from.Item2,bench,index),"編成を更新しました。");e.Handled=true;
                }
            };
        }
        return grid;
    }
    static Point BattleAnchor(bool player,int slot)
    {
        double x=(player?318:825)+slot%4*92;
        double y=slot<4?327:501;
        return new Point(x,y);
    }
    void UpdateBattleActors()
    {
        foreach(var f in game.Fighters)
        {
            if(!actors.TryGetValue((f.Player,f.Slot),out var old))continue;
            var point=BattleAnchor(f.Player,f.Slot);var parent=(Canvas?)VisualTreeHelper.GetParent(old)??scene;parent.Children.Remove(old);
            var actor=Actor(f.Unit,f,false,f.Slot,true);
            actors[(f.Player,f.Slot)]=actor;Put(parent,actor,point.X-48,point.Y-143,96,174);
        }
    }
    void PlayCue(BattleCue? cue)
    {
        if(cue==null)return;
        Sound(cue.IsSkill?"skill":"hit");
        var origin=BattleAnchor(cue.Player,cue.Slot);
        if(!preferences.ReducedMotion&&actors.TryGetValue((cue.Player,cue.Slot),out var actor))
        {
            var transform=(TranslateTransform)actor.RenderTransform;
            Animate(transform,TranslateTransform.XProperty,new DoubleAnimation(0,cue.Player?16:-16,TimeSpan.FromMilliseconds(110)) {AutoReverse=true});
        }
        if(cue.IsSkill)Float(origin.X-65,origin.Y-170,SkillName(cue.Skill),Gold,17);
        foreach(var impact in cue.Impacts)
        {
            var target=BattleAnchor(impact.Player,impact.Slot);
            var color=impact.Kind=="heal"?B("#7CF2B7"):impact.Kind=="shield"?Cyan:cue.IsSkill?Gold:White;
            if(!preferences.ReducedMotion)
            {
                ImpactEffect(target,impact.Kind,cue.IsSkill,cue.Skill);
                var beam=new Line {X1=origin.X,Y1=origin.Y-70,X2=target.X,Y2=target.Y-70,Stroke=color,StrokeThickness=cue.IsSkill?4:2,Opacity=.9};
                effects.Children.Add(beam);FadeOut(beam,260);
                var burst=new Ellipse {Stroke=color,StrokeThickness=2,RenderTransformOrigin=new Point(.5,.5),RenderTransform=new ScaleTransform(.3,.3)};
                Put(effects,burst,target.X-27,target.Y-97,54,54);
                var scale=(ScaleTransform)burst.RenderTransform;
                Animate(scale,ScaleTransform.ScaleXProperty,new DoubleAnimation(.3,1.6,TimeSpan.FromMilliseconds(320)));
                Animate(scale,ScaleTransform.ScaleYProperty,new DoubleAnimation(.3,1.6,TimeSpan.FromMilliseconds(320)));
                FadeOut(burst,340);
            }
            Float(target.X-32,target.Y-110,(impact.Kind=="heal"?"+":impact.Kind=="shield"?"盾 ":"")+impact.Amount.ToString("0"),color,cue.IsSkill?27:21);
        }
    }
    static string SkillName(string skill)=>skill switch {"strike"=>"強撃","aoe"=>"全体攻撃","heal"=>"治癒","guard"=>"鉄壁","gold"=>"集金","bigmoney"=>"財力",_=>"スキル"};
    void Float(double x,double y,string text,Brush color,double size)
    {
        var label=Label(text,size,color,true);
        label.Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Colors.Black,BlurRadius=4,ShadowDepth=2,Opacity=.95};
        Put(effects,label,x,y,180,44);
        if(!preferences.ReducedMotion)
            Animate(label,Canvas.TopProperty,new DoubleAnimation(y,y-40,TimeSpan.FromMilliseconds(650)));
        FadeOut(label,preferences.ReducedMotion?350:750);
    }
    void FadeOut(UIElement element,double milliseconds)
    {
        var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(Math.Max(70,milliseconds))) {BeginTime=TimeSpan.FromMilliseconds(80)};
        fade.Completed+=(_,_)=>effects.Children.Remove(element);
        Animate(element,OpacityProperty,fade);
    }
    void Toast(string heading,string subtitle,Brush color)
    {
        var box=new Border {Background=B("#EA0B1228"),BorderBrush=color,BorderThickness=new Thickness(0,1,0,1),IsHitTestVisible=false};
        var stack=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        var title=Label(heading,39,color,true);title.TextAlignment=TextAlignment.Center;stack.Children.Add(title);
        var sub=Label(subtitle,16,White);sub.TextAlignment=TextAlignment.Center;stack.Children.Add(sub);box.Child=stack;
        Put(effects,box,267,300,915,130);
        if(!preferences.ReducedMotion)
        {
            box.RenderTransform=new TranslateTransform();
            Animate(((TranslateTransform)box.RenderTransform),TranslateTransform.XProperty,new DoubleAnimation(-80,0,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
        }
        FadeOut(box,1600);
    }
}

