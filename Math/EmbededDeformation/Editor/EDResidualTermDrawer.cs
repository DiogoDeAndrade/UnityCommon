using UnityEditor;
using UC.Editor;

#if UC_ENABLE_ED
namespace UC.ED.Editor
{
    /// <summary>
    /// Type picker for the energy model's term list. Reuses the generic managed-reference drawer,
    /// which also routes each term's own fields through NaughtyEditorGUI - so a term's ShowIf and
    /// Min attributes work inside the list.
    /// </summary>
    [CustomPropertyDrawer(typeof(EDResidualTerm), true)]
    public class EDResidualTermDrawer : BaseFunctionDrawer<EDResidualTerm>
    {
    }
}
#endif
