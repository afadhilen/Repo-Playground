using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuManager : MonoBehaviour
{
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 1f;
    public float waitBeforeLoad = 2f;

    void Start()
    {
        fadeCanvasGroup.gameObject.SetActive(false);
    }

    public void PlayGame()
    {
        StartCoroutine(FadeAndLoadScene(1));
    }

    IEnumerator FadeAndLoadScene(int sceneIndex)
    {
        fadeCanvasGroup.gameObject.SetActive(true);
        yield return StartCoroutine(Fade(0, 1, fadeDuration));

        yield return new WaitForSeconds(waitBeforeLoad);

        SceneManager.LoadScene(sceneIndex);
    }

    public void ExitGame()
    {
        Application.Quit();
    }

    IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float timer = 0;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, timer / duration);
            yield return null;
        }
        fadeCanvasGroup.alpha = endAlpha;
    }
}
