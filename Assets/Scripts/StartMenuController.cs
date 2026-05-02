using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuController : MonoBehaviour
{
    [Tooltip("开始游戏要跳转到的场景名（必须加入 Build Settings）")]
    public string nextSceneName = "LevelSelectScene";
    [Tooltip("成就藏馆要跳转到的场景名（必须加入 Build Settings）")]
    public string achievementSceneName = "AchievementScene";

    void Awake()
    {
        // 兼容旧场景：若成就按钮被置灰，运行时强制激活
        var ach = FindObjectOfType<AchievementButton>();
        if (ach != null) ach.SetActive(true);

        // 兼容旧场景：若按钮未在 Builder 中连事件，这里补上
        Button achBtn = null;
        if (ach != null) achBtn = ach.GetComponent<Button>();
        if (achBtn == null)
        {
            var go = GameObject.Find("成就藏馆");
            if (go != null) achBtn = go.GetComponent<Button>();
        }
        if (achBtn != null)
        {
            achBtn.onClick.AddListener(OpenAchievementMuseum);
        }
    }

    public void StartGame()
    {
        SceneTransitioner.Instance.LoadSceneWithFade(nextSceneName, 0.6f);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenAchievementMuseum()
    {
        AchievementPageController.SetReturnTarget(SceneManager.GetActiveScene().name, false);
        SceneTransitioner.Instance.LoadSceneWithFade(achievementSceneName, 0.6f);
    }
}
