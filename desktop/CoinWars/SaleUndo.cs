using System.Windows;
using System.Windows.Controls;
namespace CoinWars;
public partial class GameWindow
{
    void RenderSaleUndo()
    {
        undoHost.Children.Clear();
        if(!game.CanUndoSale)return;
        var undo=Btn("売却を取り消す",()=>
        {
            if(!Planning)return;
            if(game.UndoSale()){selected=null;selectedItem=null;message="売却前のコイン・キャラ・装備に戻しました。";Persist();Render();}
        },Planning,true);
        undo.ToolTip=game.SoldName+"の売却を取り消す。他の操作でゲームの状態が変わるまで有効。";
        undo.Width=260;undo.Height=48;undoHost.Children.Add(undo);
    }
}
