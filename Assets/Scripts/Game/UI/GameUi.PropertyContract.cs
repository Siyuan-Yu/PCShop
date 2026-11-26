using System;
using System.Collections.Generic;
using PCShopSell.Core.Common;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    public sealed partial class GameUi
    {
        private Window _propertyContract;
        private readonly Queue<Action> _propertyContractPopups = new Queue<Action>();
        private bool PropertyContractOpen => _propertyContract != null && !_propertyContract.Closed;

        private void ShowPendingPropertyContractPopup()
        {
            if (!PropertyContractOpen && !PopupOpen && _propertyContractPopups.Count > 0)
                _propertyContractPopups.Dequeue().Invoke();
        }

        private void OpenPropertyContract(string title, string property, string details, Money due,
            Action<Transform> buildFees, string terms, bool purchase, Func<bool> sign, Action refresh,
            string confirmName, string blocker = null, Action<Transform> referral = null, int height = 610)
        {
            bool submitted = false;
            _propertyContract = OpenWindow(title, 760, height, contract =>
            {
                var sheet = Ui.PaperPanel(contract.Content, "ContractPaper", lifted: true);
                var content = sheet.rectTransform;
                Ui.VList(content, 6, 18);
                Ui.Trim(content, "ContractPaperEdge", Ui.FlatWellEdge, 2);
                var heading = Ui.Label(content, title, 32, TextAnchor.MiddleCenter);
                heading.fontStyle = FontStyle.Bold;
                Ui.Size(heading, -1, 42);
                Ui.Size(Ui.Label(content, S.InAgency ? S.CurrentPlace.Name + "经办" : "房产中介经办",
                    15, TextAnchor.MiddleCenter, Ui.Dim), -1, 22);
                Ui.Size(Ui.Panel(content, "ContractRule", Ui.FlatButtonEdge), -1, 1);

                var columns = Ui.Row(content, -1, 18);
                var subject = Ui.Size(Ui.Rect(columns, "ContractSubject"), 0, -1, 1);
                Ui.VList(subject, 4, 0);
                Line(subject, purchase ? "购买标的" : "租赁标的", 18, Ui.Accent);
                var propertyName = Para(subject, property, 23);
                propertyName.fontStyle = FontStyle.Bold;
                Para(subject, details, 16, Ui.Dim);

                var fees = Ui.Size(Ui.Rect(columns, "ContractFees"), 0, -1, 1);
                Ui.VList(fees, 4, 0);
                Line(fees, purchase ? "购买费用" : "首次应付", 18, Ui.Accent);
                buildFees(fees);

                Ui.Size(Ui.Panel(content, "PaymentRule", Ui.FlatWellEdge), -1, 1).raycastTarget = false;
                var amounts = Ui.Row(content, 55, 10);
                var payable = Ui.Size(Ui.Rect(amounts, "ContractPayment"), 0, -1, 1);
                Ui.VList(payable, 0, 0);
                Line(payable, due.IsNegative ? "本次净退回" : "本次实付", 17, Ui.TextColor);
                Line(payable, due.Abs().ToString(), 26, Ui.Good).fontStyle = FontStyle.Bold;
                string balanceLabel = due.IsPositive ? "付款后余额" : "结算后余额";
                var balance = Ui.Label(amounts, $"{balanceLabel}  {S.Ledger.Balance - due}", 18,
                    TextAnchor.LowerRight, Ui.TextColor);
                balance.fontStyle = FontStyle.Bold;
                Ui.Size(balance, 240, -1);
                Ui.Size(Ui.Panel(content, "TermsRule", Ui.FlatWellEdge), -1, 1).raycastTarget = false;

                Para(content, terms, 17);
                if (blocker != null) Para(content, blocker, 16, Ui.Warn);
                referral?.Invoke(content);
                Ui.Size(Ui.Panel(content, "SignatureRule", Ui.FlatButtonEdge), -1, 1);
                Line(content, purchase ? "买受人签名" : "承租人签名", 17, Ui.Accent);
                var signing = Ui.Row(content, 45, 12);
                var signatureBox = Ui.Size(Ui.Rect(signing, "ContractSignature"), 148, 45);
                Ui.Panel(signatureBox, "SignatureLine", Ui.FlatButtonEdge).rectTransform.Anchor(0, 0, 1, 0, 0, 0, 0, -1);
                var pending = Ui.Label(signatureBox, "待签署", 15, TextAnchor.LowerLeft, Ui.Dim);
                pending.rectTransform.Fill(4);
                var stroke = Ui.Rect(signatureBox, "SignatureInk").Fill(2).gameObject.AddComponent<ContractSignatureGraphic>();
                stroke.color = Ui.TextColor;
                stroke.raycastTarget = false;
                var stamp = Ui.Label(signing, "已签约", 21, TextAnchor.MiddleLeft, Ui.Warn);
                stamp.fontStyle = FontStyle.Bold;
                stamp.gameObject.SetActive(false);

                var actions = Ui.Row(contract.Content, 44, 10);
                var back = Ui.Size(Ui.Button(actions, "再看看", contract.Close, 18, Ui.ButtonSecondary), 176, 44);
                Button confirm = null;
                confirm = Ui.Size(Ui.Button(actions, due.IsPositive ? "确认签约并付款" : "确认签约并结算", () =>
                {
                    if (submitted || blocker != null) return;
                    submitted = true;
                    if (!sign())
                    {
                        contract.Close();
                        return;
                    }
                    var reveal = NewUiReveal();
                    if (reveal == null || contract.Closed) { contract.Close(); return; }
                    contract.Closable = false;
                    back.interactable = confirm.interactable = false;
                    balance.text = $"{balanceLabel}  {S.Ledger.Balance}";
                    pending.gameObject.SetActive(false);
                    reveal.Add(stroke.transform, 0, .68f, progress => stroke.Progress = progress);
                    reveal.Add(stamp.transform, .68f, .32f, progress =>
                    {
                        if (progress > 0) stamp.gameObject.SetActive(true);
                        if (progress >= 1f) contract.Close();
                    });
                    PlayUiReveal(reveal);
                }, 18, Ui.ButtonHot, blocker == null), -1, 44, 1);
                confirm.name = confirmName;
            }, onClosed: () =>
            {
                _propertyContract = null;
                if (submitted) refresh?.Invoke();
            }, showTitleBar: false, fitContent: true);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class ContractSignatureGraphic : MaskableGraphic
    {
        private static readonly Vector2[][] Strokes =
        {
            new[] { new Vector2(32, 31), new Vector2(33, 21), new Vector2(30, 9), new Vector2(26, 11) },
            new[] { new Vector2(21, 24), new Vector2(14, 16), new Vector2(8, 13) },
            new[] { new Vector2(42, 24), new Vector2(52, 17) },
            new[] { new Vector2(60, 26), new Vector2(89, 29) },
            new[] { new Vector2(76, 35), new Vector2(75, 24), new Vector2(71, 9) },
            new[] { new Vector2(75, 26), new Vector2(69, 19), new Vector2(60, 13) },
            new[] { new Vector2(75, 24), new Vector2(84, 17) },
            new[] { new Vector2(85, 26), new Vector2(113, 29) },
            new[] { new Vector2(101, 37), new Vector2(99, 24), new Vector2(95, 8) },
            new[] { new Vector2(99, 27), new Vector2(92, 18), new Vector2(85, 14) },
            new[] { new Vector2(99, 26), new Vector2(110, 17), new Vector2(123, 15) },
            new[] { new Vector2(19, 6), new Vector2(60, 8), new Vector2(105, 7), new Vector2(122, 10) },
        };

        private float _progress;
        public float Progress { set { _progress = value; SetVerticesDirty(); } }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float length = 0;
            foreach (var path in Strokes)
                for (int i = 1; i < path.Length; i++) length += Vector2.Distance(path[i - 1], path[i]);

            float remaining = _progress * length;
            foreach (var path in Strokes)
            {
                for (int i = 1; i < path.Length && remaining > 0; i++)
                {
                    Vector2 start = path[i - 1], end = path[i];
                    float segment = Vector2.Distance(start, end);
                    Vector2 offset = rect.min;
                    Vector2 a = start + offset;
                    Vector2 b = Vector2.Lerp(start, end, Mathf.Min(1, remaining / segment)) + offset;
                    Vector2 normal = Vector2.Perpendicular((b - a).normalized) * 1.1f;
                    int index = mesh.currentVertCount;
                    mesh.AddVert(a + normal, color, Vector2.zero);
                    mesh.AddVert(b + normal, color, Vector2.zero);
                    mesh.AddVert(b - normal, color, Vector2.zero);
                    mesh.AddVert(a - normal, color, Vector2.zero);
                    mesh.AddTriangle(index, index + 1, index + 2);
                    mesh.AddTriangle(index, index + 2, index + 3);
                    remaining -= segment;
                }
            }
        }
    }
}
