using UnityEditor;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine.SceneManagement;

public class BasicMenu : MonoBehaviour
{
    [SerializeField]
    private AudioSource mainSound;
    [SerializeField]
    private CanvasGroup titleContainer;
    [SerializeField]
    private CanvasGroup optionsContainer;
    [SerializeField]
    private CanvasGroup credits;
    [SerializeField, Scene]
    private string      gameScene;

    private bool          inCredits = false;
    private BigTextScroll creditsScroll;

    void Start()
    {
        creditsScroll = credits.GetComponent<BigTextScroll>();
        creditsScroll.onEndScroll += OnEndScroll;

        mainSound.volume = 0.0f;
        mainSound.FadeTo(0.25f, 0.5f);

        // Lock and hide the cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (inCredits)
        {
            if (Input.anyKeyDown)
            {
                EndCredits();
            }
        }
    }

    public void StartGame()
    {
        mainSound.FadeTo(0.0f, 0.5f);
        FullscreenFader.FadeOut(0.5f, Color.black, () =>
        {
            SceneManager.LoadScene(gameScene);
        });
    }

    public void Quit()
    {
        mainSound.FadeTo(0.0f, 0.5f);
        FullscreenFader.FadeOut(0.5f, Color.black, () =>
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

    public void ShowCredits()
    {
        inCredits = true;
        optionsContainer.FadeOut(0.5f);
        titleContainer.FadeOut(0.5f);
        credits.FadeIn(0.5f);
        creditsScroll.Reset();
    }

    public void EndCredits()
    {
        inCredits = false;
        optionsContainer.FadeIn(0.5f);
        titleContainer.FadeIn(0.5f);
        credits.FadeOut(0.5f);
    }

    void OnEndScroll()
    {
        creditsScroll.Reset();
    }
}
