using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace CoinWars;

/// <summary>Presentation-only effects. Never changes battle state or its random generator.</summary>
public partial class GameWindow
{
    readonly Random visualRandom = new();
    void TurnOrder()
    {
        Put(scene, Label("ACTION ORDER", 10, Muted), 280, 159, 125, 22);
        var order=game.Fighters.Where(f=>f.Alive).OrderBy(f=>Math.Max(0,100-f.Gauge)/f.Speed).Take(7).ToArray();
        for(int i=0;i<order.Length;i++)
        {
            var fighter=order[i];
            var frame=new Border {BorderBrush=fighter.Player?Cyan:B("#F3A0B4"),BorderThickness=new Thickness(i==0?2:1),
                Child=Portrait(fighter.Unit.Key),Background=B("#162037")};
            frame.ToolTip=Data.Def(fighter.Unit).Name+" / 次の行動（予測）";
            Put(scene,frame,412+i*51,151,43,46);
        }
    }
    void BattleEntrance()
    {
        if(preferences.ReducedMotion){Toast("BATTLE START",$"ROUND {game.S.Round:00}",Cyan);return;}
        var top=new Border {Background=B("#F20A1023"),BorderBrush=Cyan,BorderThickness=new Thickness(0,0,0,1)};
        var bottom=new Border {Background=B("#F20A1023"),BorderBrush=Gold,BorderThickness=new Thickness(0,1,0,0)};
        Put(effects,top,244,103,940,249);Put(effects,bottom,244,352,940,274);
        top.RenderTransform=new TranslateTransform();bottom.RenderTransform=new TranslateTransform();
        Animate(((TranslateTransform)top.RenderTransform),TranslateTransform.YProperty,
            new DoubleAnimation(0,-270,TimeSpan.FromMilliseconds(620)){BeginTime=TimeSpan.FromMilliseconds(120),EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}});
        Animate(((TranslateTransform)bottom.RenderTransform),TranslateTransform.YProperty,
            new DoubleAnimation(0,290,TimeSpan.FromMilliseconds(620)){BeginTime=TimeSpan.FromMilliseconds(120),EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}});
        FadeOut(top,760);FadeOut(bottom,760);
        var scan=new Rectangle {Fill=new LinearGradientBrush(Colors.Transparent,ColorOf("#B489F0EF"),0)};
        Put(effects,scan,246,103,8,522);
        Animate(scan,Canvas.LeftProperty,new DoubleAnimation(246,1180,TimeSpan.FromMilliseconds(650)));
        FadeOut(scan,700);
        Toast("BATTLE START",$"ROUND {game.S.Round:00}  /  自動戦闘",Cyan);
    }
    void SkillCutIn(BattleCue cue)
    {
        var fighter=game.Fighters.FirstOrDefault(f=>f.Player==cue.Player&&f.Slot==cue.Slot);
        if(fighter==null)return;
        Sound("charge");
        var accent=cue.Skill switch {"heal"=>B("#92F2C0"),"guard"=>Cyan,"gold" or "bigmoney"=>Gold,_=>B("#D6B9FF")};
        var cutin=new Canvas {ClipToBounds=true,Background=new LinearGradientBrush(ColorOf("#F20C1028"),ColorOf("#E74B3976"),0),
            RenderTransform=new TranslateTransform(cue.Player?-220:220,0)};
        Put(effects,cutin,248,250,934,230);
        Polygon(cutin,"0,0 330,0 475,230 0,230",B("#303B6A"),"#7B88BA");
        for(int i=0;i<15;i++)
        {
            double x=340+i*45;
            Line(cutin,x,0,x-120,230,i%3==0?"#7C729A":"#3B3B65",i%3==0?2:1);
        }
        var portrait=Portrait(fighter.Unit.Key,PortraitFraming.UpperBody);
        portrait.RenderTransform=new TranslateTransform();
        Put(cutin,portrait,40,0,350,280);
        Put(cutin,Label(cue.Player?"SKILL ACTIVATED":"ENEMY SKILL",12,accent,true),435,38,430,25);
        Put(cutin,Label(SkillName(cue.Skill),45,White,true),430,74,440,65);
        Put(cutin,Label(Data.Def(fighter.Unit).Name,21,accent,true),435,144,420,40);
        Line(cutin,430,193,870,193,"#D6C49C",2);
        double duration=420;
        Animate(((TranslateTransform)cutin.RenderTransform),TranslateTransform.XProperty,
            new DoubleAnimation(cue.Player?-220:220,0,TimeSpan.FromMilliseconds(140)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
        Animate(((TranslateTransform)portrait.RenderTransform),TranslateTransform.XProperty,
            new DoubleAnimation(-12,14,TimeSpan.FromMilliseconds(duration)));
        var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(110)){BeginTime=TimeSpan.FromMilliseconds(duration-80)};
        fade.Completed+=(_,_)=>effects.Children.Remove(cutin);Animate(cutin,OpacityProperty,fade);
    }
    void ImpactEffect(Point target,string kind,bool skill,string skillName)
    {
        double cx=target.X,cy=target.Y-70;
        var accent=kind=="heal"?B("#8EF3B8"):kind=="shield"?Cyan:skill?Gold:White;
        if(kind=="heal")
        {
            for(int i=0;i<5;i++)
            {
                var plus=Label("+",21,accent,true);
                double x=cx-35+visualRandom.NextDouble()*70,y=cy-20+visualRandom.NextDouble()*50;
                Put(effects,plus,x,y,25,30);
                Animate(plus,Canvas.TopProperty,new DoubleAnimation(y,y-50,TimeSpan.FromMilliseconds(500)));
                FadeOut(plus,520);
            }
            return;
        }
        if(kind=="shield")
        {
            var shield=new Polygon {Points=new PointCollection([new(0,0),new(55,0),new(55,45),new(27,68),new(0,45)]),
                Stroke=Cyan,StrokeThickness=3,Fill=B("#355CD6E9")};
            Put(effects,shield,cx-27,cy-38,55,68);FadeOut(shield,650);return;
        }
        int rays=skill?12:6;
        for(int i=0;i<rays;i++)
        {
            double angle=i*Math.PI*2/rays+visualRandom.NextDouble()*.3;
            double length=skill?55:30;
            var spark=new Line {X1=cx+Math.Cos(angle)*6,Y1=cy+Math.Sin(angle)*6,X2=cx+Math.Cos(angle)*length,Y2=cy+Math.Sin(angle)*length,
                Stroke=accent,StrokeThickness=i%3==0?3:1};
            effects.Children.Add(spark);FadeOut(spark,skill?420:240);
        }
        if(skillName=="strike"||!skill)
        {
            var slash=new Line {X1=cx-47,Y1=cy+42,X2=cx+47,Y2=cy-42,Stroke=accent,StrokeThickness=skill?7:3,
                Effect=new DropShadowEffect{Color=skill?ColorOf("#DBAF5D"):ColorOf("#7AD6ED"),BlurRadius=12,ShadowDepth=0}};
            effects.Children.Add(slash);FadeOut(slash,260);
        }
        if(skillName=="aoe"&&skill)
        {
            var lightning=new Polyline {Points=new PointCollection([new(cx-15,cy-80),new(cx+9,cy-38),new(cx-6,cy-30),new(cx+15,cy+30)]),
                Stroke=B("#D6B9FF"),StrokeThickness=4};
            effects.Children.Add(lightning);FadeOut(lightning,370);
        }
        if(skill)ShakeScene();
        // Hit reaction is local to the struck character; dead units retain a dim silhouette.
        var struck=game.Fighters.FirstOrDefault(f=>Math.Abs(BattleAnchor(f.Player,f.Slot).X-target.X)<.1&&Math.Abs(BattleAnchor(f.Player,f.Slot).Y-target.Y)<.1);
        if(struck!=null&&actors.TryGetValue((struck.Player,struck.Slot),out var actor))
        {
            var transform=(TranslateTransform)actor.RenderTransform;
            Animate(transform,TranslateTransform.XProperty,new DoubleAnimation(0,5,TimeSpan.FromMilliseconds(45)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(2)});
        }
    }
    void ShakeScene()
    {
        scene.RenderTransform??=new TranslateTransform();
        if(scene.RenderTransform is not TranslateTransform)scene.RenderTransform=new TranslateTransform();
        var shake=new DoubleAnimationUsingKeyFrames {Duration=TimeSpan.FromMilliseconds(170)};
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-3,KeyTime.FromPercent(0)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(4,KeyTime.FromPercent(.25)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(-2,KeyTime.FromPercent(.5)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromPercent(.75)));
        shake.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromPercent(1)));
        Animate(((TranslateTransform)scene.RenderTransform),TranslateTransform.XProperty,shake);
    }
    void ShowRoundResult(string result)
    {
        var accent=result=="win"?Gold:Cyan;
        var layer=new Canvas {Background=B("#EE0B1328"),ClipToBounds=true};
        Put(effects,layer,246,162,936,400);
        Line(layer,0,0,936,0,"#CFBE94",2);Line(layer,0,399,936,399,"#CFBE94",2);
        var mvp=game.Fighters.Where(f=>f.Player).MaxBy(f=>f.Dealt);
        if(mvp!=null)
        {
            Put(layer,Portrait(mvp.Unit.Key,PortraitFraming.UpperBody),25,12,300,388);
            Put(layer,Label("MVP  /  "+Data.Def(mvp.Unit).Name,17,Gold,true),368,252,520,35);
            Put(layer,Label($"与ダメージ   {mvp.Dealt:N0}",15,Muted),368,294,520,30);
        }
        Put(layer,Label(result=="win"?"VICTORY":result=="draw"?"DRAW":"DEFEAT",52,accent,true),355,48,550,90);
        Put(layer,Label(result=="win"?"投資が、勝利を引き寄せた。":result=="draw"?"時間切れ。編成を見直して次の戦いへ。":"まだ終わらない。次の一手を。",19,White),368,143,520,47);
        Put(layer,Label($"残りHP {game.S.Hp}     所持コイン {game.S.Gold}",19,White),368,201,520,35);
        if(!preferences.ReducedMotion)
        {
            layer.RenderTransform=new TranslateTransform(-85,0);
            Animate(((TranslateTransform)layer.RenderTransform),TranslateTransform.XProperty,new DoubleAnimation(-85,0,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
            for(int i=0;i<17;i++)
            {
                var particle=new Rectangle {Fill=accent,Opacity=.55,RenderTransform=new RotateTransform(45)};
                double x=visualRandom.NextDouble()*930,y=350+visualRandom.NextDouble()*50;
                Put(layer,particle,x,y,3,3);
                Animate(particle,Canvas.TopProperty,new DoubleAnimation(y,y-240,TimeSpan.FromMilliseconds(1600+i*25)));
            }
        }
        var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(220)){BeginTime=TimeSpan.FromMilliseconds(preferences.ReducedMotion?400:1530)};
        fade.Completed+=(_,_)=>effects.Children.Remove(layer);Animate(layer,OpacityProperty,fade);
    }
}
