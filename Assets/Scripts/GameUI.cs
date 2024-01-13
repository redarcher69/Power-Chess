using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance { set; get; }

    public Server server;
    public Client client;

    [SerializeField] private Animator menuAnimator;
    [SerializeField] private TMP_InputField addresInput;

    private void Awake()
    {
        Instance = this;
    }

    //Buttons
    public void OnLocalGameButton()
    {
        menuAnimator.SetTrigger("InGameMenu");
        server.Init(8007);
        client.Init("127.0.0.1", 8007);
    }   
    public void OnOnlineGameButton()
    {
        menuAnimator.SetTrigger("OnlineMenu");
    }

    public void OnOnlineHostButton()
    {
       server.Init(8007);
       client.Init("127.0.0.1", 8007);
       menuAnimator.SetTrigger("HostMenu");
    }

    public void OnOnlineConnetButton()
    {
        client.Init(addresInput.text, 8007);
        Debug.Log("Pressed OnOnlineConnetButton");
    }

    public void OnOnlineBackButton()
    {
        menuAnimator.SetTrigger("StartMenu"); 
    }

    public void OnHostBackButton()
    {
        server.ShutDown();
        client.ShutDown();
        menuAnimator.SetTrigger("OnlineMenu");
    }
}
