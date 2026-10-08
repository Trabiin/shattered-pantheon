// A placeholder hero or enemy on the battle screen: faction-coloured card with health, shield and energy bars.
namespace ShatteredPantheon.Game
{
    using System.Collections;
    using System.Linq;
    using ShatteredPantheon.Battle;
    using UnityEngine;
    using UnityEngine.UI;

    public class UnitCard
    {
        public readonly RectTransform Root;
        public readonly string Name;
        readonly Image face, hpFill, shieldFill, energyFill;
        readonly Text hpText, statusText;
        readonly CanvasGroup group;
        // Card animations run on the card itself, so they stop when the card is destroyed (on restart).
        readonly MonoBehaviour host;
        Coroutine pulse;

        public UnitCard(Unit u, RectTransform parent, Vector2 pos)
        {
            Name = u.Def.Name;
            var frame = Ui.Panel(parent, u.Key, u.Boss ? Palette.Ult : new Color(0, 0, 0, 0.6f));
            Root = frame.rectTransform;
            Root.Configure(pos, u.Boss ? new Vector2(320, 270) : new Vector2(300, 250));
            group = frame.gameObject.AddComponent<CanvasGroup>();
            host = frame.gameObject.AddComponent<CoroutineHost>();

            face = Ui.Panel(Root, "Face", Palette.Faction(u.Faction));
            Ui.Stretch(face.rectTransform, 4);

            Ui.Label(Root, u.Def.Name, 30, Color.white, TextAnchor.UpperCenter, new Vector2(0, 75), new Vector2(280, 80)).Bold().FitText(18);
            Ui.Label(Root, $"{u.Def.Role} · {u.Faction} · {u.Type}", 20, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, new Vector2(0, 22), new Vector2(290, 30));
            statusText = Ui.Label(Root, "", 20, Palette.Status, TextAnchor.MiddleCenter, new Vector2(0, -12), new Vector2(290, 30));

            hpFill = Bar(Root, new Vector2(0, -55), 30, Palette.Heal, out hpText);
            shieldFill = Ui.Panel(hpFill.transform.parent, "Shield", new Color(Palette.Shield.r, Palette.Shield.g, Palette.Shield.b, 0.7f));
            Ui.Fill(shieldFill.rectTransform, 0, 0.25f);
            hpText.transform.SetAsLastSibling();
            energyFill = Bar(Root, new Vector2(0, -95), 14, Palette.Ult, out _);
            Refresh(u);
        }

        static Image Bar(RectTransform parent, Vector2 pos, float height, Color color, out Text label)
        {
            var back = Ui.Panel(parent, "Bar", new Color(0, 0, 0, 0.55f));
            back.rectTransform.Configure(pos, new Vector2(270, height));
            var fill = Ui.Panel(back.rectTransform, "Fill", color);
            Ui.Fill(fill.rectTransform, 1, 1);
            label = Ui.Label(back.rectTransform, "", 20, Color.white, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(270, height));
            return fill;
        }

        public void Refresh(Unit u)
        {
            float hp = (float)(u.Hp / u.MaxHp);
            Ui.Fill(hpFill.rectTransform, hp, 1);
            hpFill.color = hp > 0.5f ? Palette.Heal : hp > 0.25f ? Palette.Burn : Palette.Damage;
            Ui.Fill(shieldFill.rectTransform, Mathf.Min(1, (float)(u.Shield / u.MaxHp)), 0.3f);
            Ui.Fill(energyFill.rectTransform, (float)(u.Energy / 100), 1);
            hpText.text = $"{u.Hp:0} / {u.MaxHp:0}";
            statusText.text = string.Join("  ", u.St.Select(s => s.Type == "channel" ? "RITUAL" : s.Type.ToUpperInvariant()).Distinct());
            group.alpha = u.Alive ? 1 : 0.25f;
        }

        public void Pulse(float duration)
        {
            if (pulse != null) host.StopCoroutine(pulse);
            pulse = host.StartCoroutine(PulseRoutine(Mathf.Clamp(duration * 0.6f, 0.15f, 0.5f)));
        }

        IEnumerator PulseRoutine(float duration)
        {
            for (float a = 0; a < duration; a += Time.deltaTime)
            {
                Root.localScale = Vector3.one * (1 + 0.1f * Mathf.Sin(a / duration * Mathf.PI));
                yield return null;
            }
            Root.localScale = Vector3.one;
        }

        public void Flash() => host.StartCoroutine(FlashRoutine());

        IEnumerator FlashRoutine()
        {
            Color baseColor = face.color;
            face.color = Color.Lerp(baseColor, Color.white, 0.6f);
            yield return new WaitForSeconds(0.08f);
            face.color = baseColor;
        }
    }
}
