using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerDataResetController : MonoBehaviour
{
    [SerializeField] private GameObject dialog;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelLabel;
    [SerializeField] private StartingWeaponSettingsController weaponSettings;
    private bool awaitingConfirmation;
    private bool resetting;

    public bool IsDialogOpen => dialog != null && dialog.activeSelf;

    private void OnEnable() => dialog.SetActive(false);

    public void OpenDialog()
    {
        if (resetting || IsDialogOpen) return;
        awaitingConfirmation = true;
        heading.text = "プレイヤーデータをリセット";
        message.text = "オートセーブ・進捗・解放レシピ・図鑑と\n開始武器の解放・チュートリアル履歴を初期化します。\n音量設定は保持されます。\nこの操作は取り消せません。";
        cancelLabel.text = "キャンセル";
        confirmButton.gameObject.SetActive(true);
        dialog.SetActive(true);
        // Opening the dialog must never select the destructive action by default.
        cancelButton.Select();
    }

    public async void ConfirmReset()
    {
        if (!IsDialogOpen || !awaitingConfirmation || resetting) return;
        awaitingConfirmation = false;
        resetting = true;
        try
        {
            await PlayerProgressReset.Reset();
            weaponSettings.RefreshAfterReset();
            heading.text = "リセットしました";
            message.text = "プレイヤーデータを初期状態に戻しました。\nGAMESTARTから新しく始められます。";
            confirmButton.gameObject.SetActive(false);
            cancelLabel.text = "閉じる";
            cancelButton.Select();
        }
        catch (System.Exception ex)
        {
            heading.text = "リセットできませんでした";
            message.text = "リセット処理を完了できませんでした。\n" + RunSaveService.DescribeError(ex);
            awaitingConfirmation = true;
            cancelButton.Select();
        }
        finally
        {
            resetting = false;
        }
    }

    public void CloseDialog()
    {
        if (resetting) return;
        awaitingConfirmation = false;
        dialog.SetActive(false);
        resetButton.Select();
    }

    public bool TryCloseDialog()
    {
        if (!IsDialogOpen) return false;
        CloseDialog();
        return true;
    }

    private void OnDisable()
    {
        awaitingConfirmation = false;
        if (dialog != null) dialog.SetActive(false);
    }
}
