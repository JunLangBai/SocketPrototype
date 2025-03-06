using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//根据玩家的位置让他们的模型生成移动
public class PlayerPool : MonoBehaviour
{
   public string localPlayerName;
   
   //玩家要显示的模型
   public GameObject prefab;
   //希望是根据玩家的名字找到玩家对应的模型
   Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
   
   //写一个方法，根据刚才的lsit更新场景中的模型
   public void UpdatePlayer(List<PlayerInfo> list)
   {
      Debug.Log($"收到玩家列表，数量: {list.Count}"); // ✅
      
      // 删除已离开的玩家模型
      HashSet<string> currentPlayers = new HashSet<string>();
      
      foreach (PlayerInfo p in list)
      {
         currentPlayers.Add(p.name);
         if (p.name == localPlayerName) continue; // 跳过本地玩家
            
         if (models.ContainsKey(p.name))
         {
            models[p.name].transform.position = new Vector3(p.x, p.y, p.z);
         }
         else
         {
            models[p.name] = Instantiate(prefab, new Vector3(p.x, p.y, p.z), Quaternion.identity);
         }
      }
      
      // 清理已离线的玩家
      List<string> toRemove = new List<string>();
      foreach (var key in models.Keys)
      {
         if (!currentPlayers.Contains(key))
         {
            Destroy(models[key]);
            toRemove.Add(key);
         }
      }
      foreach (var key in toRemove)
      {
         models.Remove(key);
      }
   }
   
   public static PlayerPool Instance;

   private void Awake()
   {
      if (Instance == null)
      {
         Instance = this;
      }
   }
}
