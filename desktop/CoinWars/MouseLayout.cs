using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CoinWars;

public partial class GameWindow
{
    void MouseShop()
    {
        if (!shopOpen)
        {
            Put(scene,Label("ショップを開いて雇用。キャラは選択 → 配置先、またはドラッグで移動。",17,Muted),CompactLayout?24:280,746,CompactLayout?650:880,70);
            Put(scene,Btn("ショップを開く",()=>{shopOpen=true;Render();},Planning,true),CompactLayout?760:1190,752,218,54);
            return;
        }
        Put(scene,Plate("#F2121A29","#39465B"),24,CompactLayout?730:706,CompactLayout?990:1392,CompactLayout?344:151);
        Put(scene,Label("ショップ",21,White,true),40,CompactLayout?734:716,175,34);
        Put(scene,Label(CompactLayout?"肖像で比較 / ボタンで購入":"肖像：詳細・比較\n下のボタン：購入",13,Muted),CompactLayout?230:40,CompactLayout?738:754,CompactLayout?570:175,CompactLayout?30:50);
        if(!CompactLayout)Put(scene,Label(string.Join(" / ",Data.Odds[game.S.Level].Select(n=>n+"%")),10,Gold),40,818,175,28);
        for(int i=0;i<5;i++)
        {
            int index=i;string? key=game.S.Shop[i];double x=CompactLayout?36+i%3*326:224+i*193;
            double y=CompactLayout?776+i/3*146:714,width=CompactLayout?310:184;
            var card=new Grid {ClipToBounds=true,Background=B("#202A3A")};
            if(key!=null)
            {
                var d=Data.Def(key);
                var portrait=Portrait(key);portrait.Width=CompactLayout?110:76;portrait.HorizontalAlignment=HorizontalAlignment.Left;card.Children.Add(portrait);
                var caption=new StackPanel {Margin=new Thickness(CompactLayout?116:80,5,3,2)};
                var name=Label(d.Name,CompactLayout?17:13,White,true);name.TextWrapping=TextWrapping.NoWrap;caption.Children.Add(name);
                var traits=Label(string.Join("・",d.Traits.Select(t=>Data.Traits[t])),CompactLayout?14:10,Muted);traits.TextWrapping=TextWrapping.NoWrap;caption.Children.Add(traits);
                int copies=game.Owned().Count(u=>u.Key==key&&u.Star==1);
                caption.Children.Add(Label(copies>=2?"合成可能":"所持 "+copies+"体",CompactLayout?14:12,copies>=2?Cyan:Gold));
                card.Children.Add(caption);
            }
            else card.Children.Add(Label("雇用済み",17,Muted));
            var detail=Btn("",()=>OpenComparison(index),key!=null&&Planning);
            detail.Content=card;detail.Padding=new Thickness(0);detail.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            detail.ToolTip="詳細・比較を開く（購入しません）";
            detail.BorderBrush=key==null?B("#354052"):B(CostColors[Data.Def(key).Cost]);
            System.Windows.Automation.AutomationProperties.SetName(detail,key==null?"雇用済み":Data.Def(key).Name+"の詳細・比較");
            Put(scene,detail,x,y,width,88);
            string purchaseError=game.PurchaseError(index);
            bool affordable=purchaseError.Length==0;
            var buy=Btn(key==null?"購入済み":!affordable&&Planning?purchaseError:$"購入 {Data.Def(key).Cost} ◈",()=>Buy(index),Planning&&affordable,key!=null&&affordable);
            if(!affordable){buy.ToolTip=purchaseError;ToolTipService.SetShowOnDisabled(buy,true);}
            Put(scene,buy,x,y+94,width,40);
        }
        Put(scene,Btn("更新 2 ◈",()=>Execute(game.Reroll,"ショップを更新しました。","shuffle"),Planning&&game.S.Gold>=2),CompactLayout?688:1200,CompactLayout?922:714,CompactLayout?310:199,61);
        Put(scene,Btn(game.S.Locked?"固定中 ✓":"ショップ固定",()=>{game.S.Locked=!game.S.Locked;Persist();Render();},Planning,game.S.Locked),CompactLayout?688:1200,CompactLayout?993:785,CompactLayout?310:199,63);
    }

    void MoveSelection(int destination)
    {
        if(!Planning||selected is not {} from)return;
        bool bench=destination==2;var slots=bench?game.S.Bench:game.S.Board;
        int start=destination==1?4:0,end=destination==0?4:8;
        int target=Enumerable.Range(start,end-start).FirstOrDefault(i=>slots[i]==null,-1);
        if(target<0){message="空きがありません。入れ替える場合は配置先のキャラをクリックしてください。";Render();return;}
        string error=game.Move(from.bench,from.index,bench,target);
        if(error.Length==0){selected=(bench,target);message="配置を変更しました。";Sound("click");Persist();}else message=error;
        Render();
    }

    void EnableEquipmentDrag(Button button,int index)
    {
        Point start=default;
        button.PreviewMouseLeftButtonDown+=(_,e)=>start=e.GetPosition(button);
        button.PreviewMouseMove+=(_,e)=>
        {
            if(!Planning||dragging||e.LeftButton!=MouseButtonState.Pressed)return;
            var point=e.GetPosition(button);
            if(Math.Abs(point.X-start.X)<8&&Math.Abs(point.Y-start.Y)<8)return;
            dragging=true;
            try{DragDrop.DoDragDrop(button,new DataObject("CoinWarsEquipment",index),DragDropEffects.Copy);}
            finally{dragging=false;}
            e.Handled=true;
        };
    }
}
