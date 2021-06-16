using System;
using DCL;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingHUDController : IHUD
{
    internal ILoadingHUDView view;
    internal BaseVariable<bool> loadingHUDVisible => DataStore.i.HUDs.loadingHUDVisible;
    internal BaseVariable<string> loadingHUDMessage => DataStore.i.HUDs.loadingHUDMessage;
    internal BaseVariable<float> loadingHUDPercentage => DataStore.i.HUDs.loadingHUDPercentage;

    internal virtual ILoadingHUDView CreateView() => LoadingHUDView.CreateView();

    public void Initialize()
    {
        view = CreateView();

        ClearEvents();
        loadingHUDVisible.OnChange += OnVisibleHUDChanged;
        loadingHUDMessage.OnChange += OnLoadingMessageChanged;
        loadingHUDPercentage.OnChange += OnLoadingPercentageChanged;
    }

    private void OnLoadingPercentageChanged(float current, float previous) { view?.SetPercentage(current); }

    private void OnLoadingMessageChanged(string current, string previous) { view?.SetMessage(current); }

    private void OnVisibleHUDChanged(bool current, bool previous) { SetViewVisible(current); }

    public void SetVisibility(bool visible) { loadingHUDVisible.Set(visible); }

    public void Dispose() { ClearEvents(); }

    internal void ClearEvents()
    {
        loadingHUDVisible.OnChange -= OnVisibleHUDChanged;
        loadingHUDMessage.OnChange -= OnLoadingMessageChanged;
        loadingHUDPercentage.OnChange -= OnLoadingPercentageChanged;
    }

    internal void SetViewVisible(bool isVisible) { view?.SetVisible(isVisible); }
}

public interface ILoadingHUDView
{
    void SetVisible(bool isVisible);
    void SetMessage(string message);
    void SetPercentage(float percentage);
}

public class LoadingHUDView : MonoBehaviour, ILoadingHUDView
{
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private Image loadingBar;

    public static ILoadingHUDView CreateView()
    {
        LoadingHUDView view = Instantiate(Resources.Load<GameObject>("LoadingHUD")).GetComponent<LoadingHUDView>();
        view.gameObject.name = "_LoadingHUD";
        return view;
    }

    private void Awake() { SetPercentage(0); }

    public void SetVisible(bool isVisible) { gameObject.SetActive(isVisible); }
    public void SetMessage(string message) { text.text = message; }
    public void SetPercentage(float percentage) { loadingBar.transform.localScale = new Vector3(percentage, 0, 0); }
}