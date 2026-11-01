using System.Collections.Generic;
using SpellBook.Core;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.GAS.Diagnostics;
using SpellBook.GAS.Effects;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// GAS Debugger (R16 a R18): em Play, mostra os atributos com modificadores, as tags, os efeitos ativos, as habilidades
    /// e o log de eventos do ASC escolhido, atualizados a cada 250 ms. O ASC vem do campo da janela ou, vazio, da seleção.
    /// Ações: aplicar e remover efeito, conceder e retirar habilidade, zerar recarga. Lê o ASC só pela API pública,
    /// através do <see cref="GasDebuggerModel"/>.
    /// </summary>
    internal sealed class GasDebuggerWindow : EditorWindow
    {
        private const long RefreshIntervalMs = 250;

        private readonly GasDebuggerModel _model = new GasDebuggerModel();
        private readonly List<AttributeRow> _attributes = new List<AttributeRow>();
        private readonly List<TagRow> _tags = new List<TagRow>();
        private readonly List<EffectRow> _effects = new List<EffectRow>();
        private readonly List<AbilityRow> _abilities = new List<AbilityRow>();
        private readonly List<GasEventEntry> _events = new List<GasEventEntry>();

        private AbilitySystemComponent _selectedAsc;
        private AbilitySystemComponent _asc;
        private ObjectField _ascField;
        private ObjectField _effectField;
        private ObjectField _abilityField;
        private Label _status;
        private VisualElement _content;
        private ListView _attributeList;
        private ListView _tagList;
        private ListView _effectList;
        private ListView _abilityList;
        private ListView _eventList;

        [MenuItem("Window/SpellBook/GAS Debugger")]
        public static void Open()
        {
            GetWindow<GasDebuggerWindow>("GAS Debugger");
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            Inspect(null);
        }

        private void CreateGUI()
        {
            var toolbar = new Toolbar();
            _ascField = new ObjectField("ASC")
            {
                objectType = typeof(AbilitySystemComponent),
                allowSceneObjects = true,
                tooltip = "ASC inspecionado. Vazio usa o ASC do objeto selecionado na Hierarchy.",
            };
            _ascField.style.flexGrow = 1;
            toolbar.Add(_ascField);
            toolbar.Add(new ToolbarButton(() =>
            {
                _model.Log.Clear();
                Refresh();
            }) { text = "Limpar log", tooltip = "Apaga o log de eventos." });
            toolbar.Add(new ToolbarButton(() => Application.OpenURL(DocUrls.GasDebugger)) { text = "?", tooltip = "Abre o manual do GAS Debugger." });
            rootVisualElement.Add(toolbar);

            _status = new Label();
            _status.style.paddingLeft = 4;
            _status.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(_status);

            _content = new ScrollView();
            _content.style.flexGrow = 1;
            _attributeList = AddSection("Atributos", _attributes, BindAttributeRow);
            _tagList = AddSection("Tags", _tags, BindTagRow);

            _effectList = AddSection("Efeitos", _effects, BindEffectRow);
            _effectField = new ObjectField("Efeito") { objectType = typeof(GameplayEffect), allowSceneObjects = false };
            AddActions(_effectList, _effectField,
                ("Aplicar", "Aplica o efeito do campo ao ASC, com o próprio ASC como origem.", ApplyEffect),
                ("Remover selecionado", "Remove a instância selecionada na lista.", RemoveSelectedEffect));

            _abilityList = AddSection("Habilidades", _abilities, BindAbilityRow);
            _abilityField = new ObjectField("Habilidade") { objectType = typeof(GameplayAbility), allowSceneObjects = false };
            AddActions(_abilityList, _abilityField,
                ("Conceder", "Concede a habilidade do campo no nível 1.", GrantAbility),
                ("Retirar", "Retira a habilidade do campo.", RemoveAbility),
                ("Zerar recarga", "Zera a recarga da habilidade selecionada na lista.", ResetSelectedCooldown));

            _eventList = AddSection("Log de eventos", _events, BindEventRow);
            rootVisualElement.Add(_content);

            Refresh();
            rootVisualElement.schedule.Execute(Refresh).Every(RefreshIntervalMs);
        }

        private ListView AddSection<T>(string title, List<T> rows, System.Action<VisualElement, int> bind)
        {
            var list = new ListView
            {
                itemsSource = rows,
                makeItem = () => new Label(),
                bindItem = bind,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
            };
            list.style.maxHeight = 220;
            var foldout = new Foldout { text = title, value = true };
            foldout.Add(list);
            _content.Add(foldout);
            return list;
        }

        private static void AddActions(ListView list, ObjectField field, params (string text, string tooltip, System.Action action)[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            field.style.flexGrow = 1;
            row.Add(field);
            foreach ((string text, string tooltip, System.Action action) in buttons)
            {
                row.Add(new Button(action) { text = text, tooltip = tooltip });
            }

            list.parent.Add(row);
        }

        private void OnSelectionChanged()
        {
            GameObject selected = Selection.activeGameObject;
            _selectedAsc = selected != null ? selected.GetComponent<AbilitySystemComponent>() : null;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Inspect(null);
            }
        }

        /// <summary>Troca o ASC inspecionado: sai dos eventos do anterior, entra nos do novo e recomeça o log.</summary>
        private void Inspect(AbilitySystemComponent asc)
        {
            if (ReferenceEquals(asc, _asc)) return;

            _model.Log.Detach();
            _model.Log.Clear();
            _asc = asc;
            if (asc != null)
            {
                _model.Log.Attach(asc);
            }
        }

        private void Refresh()
        {
            if (_content == null) return;

            AbilitySystemComponent fieldAsc = _ascField.value as AbilitySystemComponent;
            AbilitySystemComponent wanted = fieldAsc != null ? fieldAsc : _selectedAsc;
            Inspect(Application.isPlaying && wanted != null ? wanted : null);

            bool live = _asc != null;
            _content.SetEnabled(live);
            _status.text = !Application.isPlaying
                ? "Entre em Play para inspecionar um ASC."
                : live
                    ? $"Inspecionando {_asc.name} (tempo {_asc.TickedTime:0.00} s)."
                    : "Selecione um objeto com AbilitySystemComponent ou escolha um ASC no campo acima.";

            _model.BuildAttributeRows(live && _asc.AttributeSet != null ? _asc.AttributeSet.States : null, _attributes);
            _model.BuildTagRows(live ? _asc.TagCounts : null, _tags);
            _model.BuildEffectRows(live ? _asc.ActiveEffects : null, _effects);
            _model.BuildAbilityRows(live ? _asc : null, _abilities);

            _events.Clear();
            EventLog log = _model.Log;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                _events.Add(log[i]);
            }

            _attributeList.RefreshItems();
            _tagList.RefreshItems();
            _effectList.RefreshItems();
            _abilityList.RefreshItems();
            _eventList.RefreshItems();
        }

        private void ApplyEffect()
        {
            if (_asc == null || !(_effectField.value is GameplayEffect effect)) return;

            EffectApplicationResult result = _asc.TryApplyEffect(effect, _asc, out _);
            Debug.Log($"[GAS Debugger] {effect.name} em {_asc.name}: {result}.", _asc);
            Refresh();
        }

        private void RemoveSelectedEffect()
        {
            int index = _effectList.selectedIndex;
            if (_asc == null || index < 0 || index >= _effects.Count) return;

            _asc.RemoveEffect(_effects[index].Instance);
            Refresh();
        }

        private void GrantAbility()
        {
            if (_asc == null || !(_abilityField.value is GameplayAbility ability)) return;

            _asc.GiveAbility(ability);
            Refresh();
        }

        private void RemoveAbility()
        {
            if (_asc == null || !(_abilityField.value is GameplayAbility ability)) return;

            _asc.RemoveAbility(ability);
            Refresh();
        }

        private void ResetSelectedCooldown()
        {
            int index = _abilityList.selectedIndex;
            if (_asc == null || index < 0 || index >= _abilities.Count) return;

            _asc.ResetCooldown(_abilities[index].Ability);
            Refresh();
        }

        private void BindAttributeRow(VisualElement row, int index)
        {
            AttributeRow r = _attributes[index];
            ((Label)row).text = r.IsModifier
                ? $"    {r.Operation} {r.ModifierValue:0.##}  de {SourceName(r.Source)}"
                : $"{NameOf(r.Definition)}: atual {r.CurrentValue:0.##}, base {r.BaseValue:0.##}  [{r.MinValue:0.##}, {r.MaxValue:0.##}]";
        }

        private void BindTagRow(VisualElement row, int index)
        {
            TagRow r = _tags[index];
            ((Label)row).text = $"{r.Tag.Name} ×{r.Count}";
        }

        private void BindEffectRow(VisualElement row, int index)
        {
            EffectRow r = _effects[index];
            string time = r.Effect != null && r.Effect.DurationPolicy == EffectDurationPolicy.Infinite
                ? "infinito"
                : $"{r.TimeRemaining:0.0} de {r.AppliedDuration:0.0} s";
            ((Label)row).text = $"{NameOf(r.Effect)} de {NameOf(r.Source)}: {time}, {r.Stacks} pilha(s)";
        }

        private void BindAbilityRow(VisualElement row, int index)
        {
            AbilityRow r = _abilities[index];
            string active = r.IsActive ? ", ativa" : "";
            string cooldown = r.CooldownRemaining > 0f ? $"recarga {r.CooldownRemaining:0.0} s ({r.CooldownNormalized:P0})" : "pronta";
            ((Label)row).text = $"{NameOf(r.Ability)} (nível {r.Level}{active}): {cooldown}";
        }

        private void BindEventRow(VisualElement row, int index)
        {
            GasEventEntry e = _events[index];
            string subject = e.Effect != null ? NameOf(e.Effect.Effect) : e.Ability != null ? NameOf(e.Ability.Ability) : "-";
            string detail = e.Kind switch
            {
                GasEventKind.EffectRemoved => $" ({e.RemovalReason})",
                GasEventKind.EffectStacksChanged => $" ({e.OldStacks} → {e.NewStacks})",
                GasEventKind.CooldownChanged => $" ({e.CooldownChange})",
                _ => "",
            };
            ((Label)row).text = $"{e.Time:0.00} s  {e.Kind}  {subject}{detail}";
        }

        private static string SourceName(object source)
        {
            return source switch
            {
                null => "(sem fonte)",
                ActiveEffectInstance instance => NameOf(instance.Effect),
                Object unityObject => NameOf(unityObject),
                _ => source.ToString(),
            };
        }

        private static string NameOf(Object target)
        {
            return target != null ? target.name : "(nenhum)";
        }
    }
}
