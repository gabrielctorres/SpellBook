using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Editor
{
    /// <summary>
    /// Drawer de todo <see cref="ScalableFloat"/> (R3, R29): seletor de modo, texto e lista do array, curva, a lista de
    /// nomes da tabela no modo <c>Table</c> (sem texto livre) e a prévia "nível → valor".
    /// </summary>
    [CustomPropertyDrawer(typeof(ScalableFloat))]
    public sealed class ScalableFloatDrawer : PropertyDrawer
    {
        public const string ModeName = "spellbook-scalable-mode";
        public const string ArraySectionName = "spellbook-scalable-array";
        public const string ArrayTextName = "spellbook-scalable-array-text";
        public const string ArrayErrorName = "spellbook-scalable-array-error";
        public const string TableSectionName = "spellbook-scalable-table";
        public const string TableNameName = "spellbook-scalable-table-name";
        public const string PreviewName = "spellbook-scalable-preview";

        private const string NeedsUIToolkit = "Este campo precisa do Inspector em UI Toolkit";

        // Habilidades do projeto, por varredura do AssetDatabase; refeita quando o projeto muda.
        private static List<GameplayAbility> _projectAbilities;

        static ScalableFloatDrawer()
        {
            EditorApplication.projectChanged += () => _projectAbilities = null;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new ScalableFloatField(property, ProjectAbilities, preferredLabel);
        }

        /// <summary>Fallback IMGUI único (ADR 0026, item 3): uma linha com o aviso.</summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.HelpBox(position, NeedsUIToolkit, MessageType.Warning);
        }

        private static IEnumerable<GameplayAbility> ProjectAbilities()
        {
            if (_projectAbilities != null) return _projectAbilities;

            _projectAbilities = new List<GameplayAbility>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(GameplayAbility)))
            {
                var ability = AssetDatabase.LoadAssetAtPath<GameplayAbility>(AssetDatabase.GUIDToAssetPath(guid));
                if (ability != null) _projectAbilities.Add(ability);
            }

            return _projectAbilities;
        }

        /// <summary>
        /// Habilidades cuja tabela vale para um campo do <paramref name="owner"/>: a própria habilidade, ou as que usam o
        /// efeito (recarga, custo e efeitos das ações das três listas, por <see cref="GameplayAbility.CollectActionEffects"/>).
        /// </summary>
        public static List<GameplayAbility> TableOwners(Object owner, IEnumerable<GameplayAbility> abilities)
        {
            var owners = new List<GameplayAbility>();
            if (owner is GameplayAbility self)
            {
                owners.Add(self);
                return owners;
            }

            if (!(owner is GameplayEffect effect) || abilities == null) return owners;

            var effects = new List<GameplayEffect>();
            foreach (GameplayAbility ability in abilities)
            {
                if (ability == null) continue;

                effects.Clear();
                effects.Add(ability.CooldownEffect);
                effects.Add(ability.CostEffect);
                ability.CollectActionEffects(effects);

                if (effects.Contains(effect)) owners.Add(ability);
            }

            return owners;
        }

        /// <summary>Nomes das tabelas das <paramref name="owners"/>, sem repetição, em ordem ordinal.</summary>
        public static List<string> TableNames(IEnumerable<GameplayAbility> owners)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (GameplayAbility ability in owners)
            {
                using var serialized = new SerializedObject(ability);
                SerializedProperty entries = serialized.FindProperty("values.entries");
                for (int i = 0; i < entries.arraySize; i++)
                {
                    string name = entries.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                    if (!string.IsNullOrEmpty(name)) names.Add(name);
                }
            }

            return new List<string>(names);
        }
    }

    /// <summary>Elemento do <see cref="ScalableFloatDrawer"/>, ligado a uma propriedade <see cref="ScalableFloat"/>.</summary>
    public sealed class ScalableFloatField : VisualElement
    {
        private readonly SerializedObject _serializedObject;
        private readonly string _path;
        private readonly List<GameplayAbility> _owners;
        private readonly EnumField _mode;
        private readonly VisualElement _constant;
        private readonly VisualElement _array;
        private readonly TextField _arrayText;
        private readonly HelpBox _arrayError;
        private readonly VisualElement _curve;
        private readonly VisualElement _table;
        private readonly DropdownField _tableName;
        private readonly Label _preview;

        public ScalableFloatField(SerializedProperty property, Func<IEnumerable<GameplayAbility>> abilities, string label = null)
        {
            _serializedObject = property.serializedObject;
            _path = property.propertyPath;
            _owners = ScalableFloatDrawer.TableOwners(_serializedObject.targetObject, abilities?.Invoke());

            var foldout = new Foldout { text = label ?? property.displayName, value = true };
            Add(foldout);

            SerializedProperty modeProperty = property.FindPropertyRelative("mode");
            _mode = new EnumField("Modo", (ScalableFloat.ScalableMode)modeProperty.enumValueIndex) { name = ScalableFloatDrawer.ModeName };
            _mode.RegisterValueChangedCallback(evt => SetMode((ScalableFloat.ScalableMode)evt.newValue));
            foldout.Add(_mode);

            _constant = new PropertyField(property.FindPropertyRelative("constant"), "Valor");
            foldout.Add(_constant);

            _array = new VisualElement { name = ScalableFloatDrawer.ArraySectionName };
            _arrayText = new TextField("Valores por nível") { name = ScalableFloatDrawer.ArrayTextName, isDelayed = true, tooltip = "Ex.: 10 8 6 4 (espaço, vírgula ou ponto e vírgula; ponto decimal)." };
            _arrayText.RegisterValueChangedCallback(evt => SubmitArrayText(evt.newValue));
            _arrayError = new HelpBox(string.Empty, HelpBoxMessageType.Error) { name = ScalableFloatDrawer.ArrayErrorName };
            _array.Add(_arrayText);
            _array.Add(_arrayError);
            _array.Add(new PropertyField(property.FindPropertyRelative("values"), "Lista"));
            foldout.Add(_array);

            _curve = new PropertyField(property.FindPropertyRelative("curve"), "Curva");
            foldout.Add(_curve);

            _table = new VisualElement { name = ScalableFloatDrawer.TableSectionName };
            _tableName = new DropdownField("Nome") { name = ScalableFloatDrawer.TableNameName, choices = ScalableFloatDrawer.TableNames(_owners) };
            _tableName.SetValueWithoutNotify(property.FindPropertyRelative("tableName").stringValue);
            _tableName.RegisterValueChangedCallback(evt => Write(p => p.FindPropertyRelative("tableName").stringValue = evt.newValue));
            _table.Add(_tableName);
            if (_owners.Count == 0)
            {
                _table.Add(new HelpBox("Nenhuma habilidade do projeto usa este efeito: não há nomes de tabela para escolher.", HelpBoxMessageType.Info));
            }

            _table.Add(new PropertyField(property.FindPropertyRelative("fallback"), "Fallback"));
            foldout.Add(_table);

            _preview = new Label { name = ScalableFloatDrawer.PreviewName };
            foldout.Add(_preview);

            SetArrayError(null);
            Refresh(property);
            this.TrackPropertyValue(property, Refresh);
        }

        /// <summary>
        /// Lê o texto do array (<see cref="ScalableFloat.TryParse"/>) e grava os valores. Texto inválido mostra o erro e
        /// não toca no valor salvo (CA5).
        /// </summary>
        public void SubmitArrayText(string text)
        {
            if (!ScalableFloat.TryParse(text, out float[] values, out string error))
            {
                SetArrayError(error);
                return;
            }

            SetArrayError(null);
            Write(property =>
            {
                SerializedProperty list = property.FindPropertyRelative("values");
                list.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).floatValue = values[i];
                }
            });
        }

        private void SetMode(ScalableFloat.ScalableMode mode)
        {
            Write(property => property.FindPropertyRelative("mode").enumValueIndex = (int)mode);
        }

        private void Write(Action<SerializedProperty> change)
        {
            SerializedProperty property = _serializedObject.FindProperty(_path);
            if (property == null) return;

            _serializedObject.Update();
            change(property);
            _serializedObject.ApplyModifiedProperties();
            Refresh(property);
        }

        private void SetArrayError(string error)
        {
            _arrayError.text = error ?? string.Empty;
            Show(_arrayError, error != null);
        }

        private void Refresh(SerializedProperty property)
        {
            var mode = (ScalableFloat.ScalableMode)property.FindPropertyRelative("mode").enumValueIndex;
            _mode.SetValueWithoutNotify(mode);
            Show(_constant, mode == ScalableFloat.ScalableMode.Constant);
            Show(_array, mode == ScalableFloat.ScalableMode.Array);
            Show(_curve, mode == ScalableFloat.ScalableMode.Curve);
            Show(_table, mode == ScalableFloat.ScalableMode.Table);

            // Com um erro na tela, o texto fica como o usuário digitou
            if (_arrayError.style.display.value == DisplayStyle.None)
            {
                _arrayText.SetValueWithoutNotify(ArrayText(property.FindPropertyRelative("values")));
            }

            _preview.text = Preview(property.boxedValue as ScalableFloat);
        }

        private static string ArrayText(SerializedProperty list)
        {
            var text = new StringBuilder();
            for (int i = 0; i < list.arraySize; i++)
            {
                if (i > 0) text.Append(' ');
                text.Append(list.GetArrayElementAtIndex(i).floatValue.ToString("R", CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        // Uma linha "nível → valor" até o MaxLevel das habilidades donas; sem dona, até o tamanho do array (mínimo 1).
        // No modo Table, avalia na tabela da primeira dona.
        private string Preview(ScalableFloat value)
        {
            if (value == null) return string.Empty;

            int maxLevel = 0;
            foreach (GameplayAbility owner in _owners)
            {
                maxLevel = Math.Max(maxLevel, owner.MaxLevel);
            }

            if (maxLevel == 0)
            {
                maxLevel = Math.Max(1, _serializedObject.FindProperty(_path + ".values")?.arraySize ?? 1);
            }

            AbilityValues table = _owners.Count > 0 ? _owners[0].Values : null;
            var text = new StringBuilder("Prévia:");
            for (int level = 1; level <= maxLevel; level++)
            {
                // Sem o nome na tabela, a prévia mostra o fallback sem o aviso do Console (o validador acusa)
                float result = value.Mode != ScalableFloat.ScalableMode.Table ? value.Evaluate(level)
                    : table != null && table.TryGetValue(value.TableName, level, out float named) ? named : value.Fallback;
                text.Append(level == 1 ? "  " : "   ").Append(level).Append(" → ")
                    .Append(result.ToString("0.###", CultureInfo.CurrentCulture));
            }

            return text.ToString();
        }

        private static void Show(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
