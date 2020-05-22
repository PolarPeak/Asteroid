using System;
using System.Collections.Generic;
using UnityEngine;
using BestHTTP.WebSocket;
using LitJson;

public class MainController : MonoBehaviour
{

    private int middleX = 0;
    private int middleZ = 0;
    private int R = 20;     // 半径

    // GameObject
    private Dictionary<string, GameObject> planetGroup = new Dictionary<string, GameObject>();
    private UnityEngine.Object planetObj;

    public Camera mainCamera;       // 主摄像机
    public GameObject[] flash;      // 炮弹结合
    public GameObject centerCube;   // 中间参照定位方块
    public GameObject AnimateRock;  // 彩蛋动画陨石

    // UI
    public GameObject timeText;
    public GameObject roundText;
    public GameObject messageBoard;

    // WebSocket
    private WebSocket webSocket;
    private int round = 1;
    public int roundTime = 300; //默认 300s 一轮
    public string url = "ws://localhost:8085/hctf2019";

    // Start is called before the first frame update
    void Start()
    {
        this.planetObj = Resources.Load("Planet");
        this.timeText.GetComponent<TimeController>().timeSecond = this.roundTime;

        // Init websocket
        webSocket = new WebSocket(new Uri(url));
        webSocket.OnOpen = OnOpen;
        webSocket.OnMessage = OnMessageReceived;
        webSocket.OnError = OnError;
        webSocket.OnClosed = OnClosed;
        webSocket.Open();
    }


    void OnOpen(WebSocket ws)
    {
        Debug.Log("connecting...");
    }

    // 接受信息
    void OnMessageReceived(WebSocket ws, string msg)
    {
        JsonData recieveData = JsonMapper.ToObject(msg);

        switch ((string)recieveData["Type"])
        {
            case "init":
                // 初始化队伍星球
                JsonData teams = recieveData["Data"];
                int count = teams.Count;
                float singleAngle = 360 / count;

                for (int i = 0; i < count; i++)
                {
                    // 星球排列成圆形
                    Vector3 position = new Vector3(middleX + this.R * Mathf.Sin(i * (singleAngle / 180 * Mathf.PI)), .0f, middleZ + this.R * Mathf.Cos(i * (singleAngle / 180 * Mathf.PI)));
                    GameObject planet = Instantiate(this.planetObj, position, Quaternion.identity) as GameObject;
                    planet.GetComponent<SinglePlanet>().SetPosition(position);
                    planet.GetComponent<SinglePlanet>().SetTeamName((string)teams[i]["name"]);
                    planet.GetComponent<SinglePlanet>().SetScoreRank((int)teams[i]["rank"], (int)teams[i]["score"]);     // 设置初始分数、排名

                    // 不显示状态
                    planet.GetComponent<SinglePlanet>().SetAttack(false);
                    planet.GetComponent<SinglePlanet>().SetDown(false);

                    planet.GetComponent<SinglePlanet>().MainCamera = this.mainCamera;       // 设置主摄像机
                    planet.GetComponent<SinglePlanet>().CenterCube = this.centerCube;       // 设置中间点方块

                    //使用队伍 ID 作为索引
                    this.planetGroup[i.ToString()] = planet;
                }

                break;

            case "attack":
                // 接收到信息，发射炮弹
                string from = (string)recieveData["Data"]["From"];
                string to = (string)recieveData["Data"]["To"];

                // 随机选取一种炮弹效果
                int flashIndex = UnityEngine.Random.Range(0, this.flash.Length);

                // 为了视觉效果，分批发射 5 次，每次间隔 0.1s
                NewAttack(this.planetGroup[from], this.planetGroup[to], flashIndex);
                StartCoroutine(DelayToInvoke.DelayToInvokeDo(() =>
                {
                    NewAttack(this.planetGroup[from], this.planetGroup[to], flashIndex);
                }, 0.2f));
                StartCoroutine(DelayToInvoke.DelayToInvokeDo(() =>
                {
                    NewAttack(this.planetGroup[from], this.planetGroup[to], flashIndex);
                }, 0.3f));
                StartCoroutine(DelayToInvoke.DelayToInvokeDo(() =>
                {
                    NewAttack(this.planetGroup[from], this.planetGroup[to], flashIndex);
                }, 0.4f));
                StartCoroutine(DelayToInvoke.DelayToInvokeDo(() =>
                {
                    NewAttack(this.planetGroup[from], this.planetGroup[to], flashIndex);
                }, 0.5f));
                break;

            case "status":
                // 设置队伍状态
                string teamId = (string)recieveData["Data"]["Id"];
                string status = (string)recieveData["Data"]["Status"];
                SetStatus(teamId, status);
                break;

            case "round":
                // 设置回合数
                this.round = int.Parse((string)recieveData["Data"]["Round"]);
                roundText.GetComponent<UnityEngine.UI.Text>().text = "第 " + this.round + " 轮";
                break;

            case "time":
                // 设置剩余时间
                int time = int.Parse((string)recieveData["Data"]["Time"]);
                timeText.GetComponent<TimeController>().SetTime(time);
                break;

            case "clear":
                teamId = (string)recieveData["Data"]["Id"];
                this.planetGroup[teamId].GetComponent<SinglePlanet>().SetDown(false);
                this.planetGroup[teamId].GetComponent<SinglePlanet>().SetAttack(false);
                break;

            case "clearAll":
                // 清空所有队伍状态
                for (int i = 1; i <= 12; i++)
                {
                    this.planetGroup[i.ToString()].GetComponent<SinglePlanet>().SetDown(false);
                    this.planetGroup[i.ToString()].GetComponent<SinglePlanet>().SetAttack(false);
                }
                break;

            // 陨石彩蛋
            case "easterEgg":
                this.AnimateRock.GetComponent<Animator>().Play("rock", -1, 0f);
                break;
        }
    }

    void OnClosed(WebSocket ws, UInt16 code, string msg)
    {
        Debug.Log(msg);
    }

    void OnError(WebSocket ws, Exception ex)
    {
        Debug.Log(string.Format("Status Code from Server: {0} and Message: {1}", ws.InternalRequest.Response.StatusCode, ws.InternalRequest.Response.Message));
    }

    // 设置队伍状态
    void SetStatus(string TeamID, string Status)
    {
        // 清空状态
        planetGroup[TeamID].GetComponent<SinglePlanet>().SetAttack(false);
        planetGroup[TeamID].GetComponent<SinglePlanet>().SetDown(false);

        switch (Status)
        {
            case "down":
                planetGroup[TeamID].GetComponent<SinglePlanet>().SetDown(true);
                break;
            case "attacked":
                planetGroup[TeamID].GetComponent<SinglePlanet>().SetAttack(true);
                break;
        }
    }


    // 抛物线攻击
    void NewAttack(GameObject From, GameObject To, int flashIndex = 0)
    {
        Vector3[] paths = new Vector3[3];

        int speed = 500;

        // 炮弹实例
        GameObject projectile;
        projectile = Instantiate(this.flash[flashIndex], From.GetComponent<Transform>().position, Quaternion.identity) as GameObject;

        // 忽略炮弹与炮弹之间的碰撞
        Physics.IgnoreLayerCollision(9, 9);

        // 忽略发射者与炮弹的碰撞
        Physics.IgnoreCollision(From.GetComponent<Collider>(), projectile.GetComponent<Collider>());

        paths[0] = From.GetComponent<Transform>().position;
        paths[2] = To.GetComponent<Transform>().position;
        paths[1] = new Vector3((paths[2].x + paths[0].x) / 2, 5, (paths[2].z + paths[0].z) / 2);

        projectile.GetComponent<Rigidbody>().AddForce(projectile.transform.forward * speed);

        iTween.MoveTo(projectile, iTween.Hash("path", paths, "movetopath", true, "orienttopath", true, "time", 1, "easetype", iTween.EaseType.linear));

        // 设置状态为被攻陷
        To.GetComponent<SinglePlanet>().SetAttack(true);
    }


    // Update is called once per frame
    void Update()
    {
        // 更新下一轮
        if (this.timeText.GetComponent<TimeController>().timeSecond == 0)
        {
            this.timeText.GetComponent<TimeController>().timeSecond = this.roundTime;
            this.round++;
            this.roundText.GetComponent<UnityEngine.UI.Text>().text = "第 " + this.round.ToString() + " 轮";
        }
    }
}