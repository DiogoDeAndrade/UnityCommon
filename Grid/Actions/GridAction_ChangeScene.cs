using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using NaughtyAttributes;

public class GridAction_ChangeScene : GridAction
{
    [SerializeField, Scene] private string sceneName;

    protected override void ActualGatherActions(GridObject subject, Vector2Int position, List<GridAction> actions)
    {
        actions.Add(this);
    }

    protected override bool ActualRunAction(GridObject subject, Vector2Int position)
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
