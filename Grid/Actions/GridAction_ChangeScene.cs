using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NaughtyAttributes;

public class GridAction_ChangeScene : GridActionContainer
{
    [SerializeField, Scene] private string sceneName;

    public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
    {
        retActions.Add(new NamedAction
        {
            name = verb,
            action = RunAction,
            container = this
        });
    }

    protected virtual bool RunAction(GridObject subject, Vector2Int position)
    {
        if (FullscreenFader.hasFader)
        {
            FullscreenFader.FadeOut(0.5f, Color.black, () =>
            {
                SceneManager.LoadScene(sceneName);
            });
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }

        return true;
    }
}
