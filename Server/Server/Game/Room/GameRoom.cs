﻿using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.Data;
using System;
using System.Collections.Generic;

namespace Server.Game
{
    public class GameRoom : JobSerializer
    {
        public int RoomId { get; set; }

        Dictionary<int, Player> _players = new Dictionary<int, Player>();
        Dictionary<int, Monster> _monsters = new Dictionary<int, Monster>();
        Dictionary<int, Projectile> _projectiles = new Dictionary<int, Projectile>();

        public Map Map { get; private set; } = new Map();

        
        //static은 GameRoom에서 공용으로 사용해야하고 객체에 포함하는 함수가 아니기 때문에
        //개인적으로 접근하는것은 불가능하다
        //public static void Init(GameRoom room, int mapId)
        //{
        //    room.Map.LoadMap(mapId);

        //    Monster monster = ObjectManager.Instance.Add<Monster>();
        //    monster.CellPos = new Vector2Int(5, 5);
        //    room.EnterGame(monster);
        //}
        
        public void Init(int mapId)
        {
            Map.LoadMap(mapId);

            Monster monster = ObjectManager.Instance.Add<Monster>();
            monster.CellPos = new Vector2Int(5, 5);
            EnterGame(monster);
        }

        public void Update()
        {

            foreach(Monster monster in _monsters.Values)
            {
                monster.Update();
            }

            foreach(Projectile projectile in _projectiles.Values)
            {
                projectile.Update();
            }

            Flush();
            
        }

        public void EnterGame(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            GameObjectType type = ObjectManager.GetObjectTypeById(gameObject.Id);

            //경합해도 잘 들어오도록           
            if(type == GameObjectType.Player)
            {
                Player player = gameObject as Player;
                _players.Add(gameObject.Id, player);
                player.Room = this;

                Map.ApplyMove(player, new Vector2Int(player.CellPos.x, player.CellPos.y));

                //본인한테 정보 전송
                {
                    S_EnterGame enterPacket = new S_EnterGame();
                    enterPacket.Player = player.Info;
                    player.Session.Send(enterPacket);

                    S_Spawn spawnPacket = new S_Spawn();
                    foreach (Player p in _players.Values)
                    {
                        if (player != p)
                            spawnPacket.Objects.Add(p.Info);
                    }

                    foreach (Monster m in _monsters.Values)
                        spawnPacket.Objects.Add(m.Info);

                    foreach (Projectile p in _projectiles.Values)
                        spawnPacket.Objects.Add(p.Info);

                    player.Session.Send(spawnPacket);
                }
            }
            else if (type == GameObjectType.Monster)
            {
                Monster monster = gameObject as Monster;
                _monsters.Add(gameObject.Id, monster);
                monster.Room = this;

                Map.ApplyMove(monster, new Vector2Int(monster.CellPos.x, monster.CellPos.y));
            }
            else if(type == GameObjectType.Projectile)
            {
                Projectile projectile = gameObject as Projectile;
                _projectiles.Add(gameObject.Id, projectile);
                projectile.Room = this;
            }
                
            //타인한테 정보 전송
            {
                S_Spawn spawnPacket = new S_Spawn();
                spawnPacket.Objects.Add(gameObject.Info);
                foreach (Player p in _players.Values)
                {
                    if (p.Id != gameObject.Id)
                    {
                        p.Session.Send(spawnPacket);
                        Map.ApplyMove(p, new Vector2Int(p.CellPos.x, p.CellPos.y));
                    }
                           
                }
            }
            

        }        

        public void LeaveGame(int objectId)
        {
            GameObjectType type = ObjectManager.GetObjectTypeById(objectId);


            if(type == GameObjectType.Player)
            {
                Player player = null;
                if (_players.Remove(objectId, out player) == false)
                    return;

                player.OnLeaveGame();
                Map.ApplyLeave(player);
                //먼저 null로 밀어버리면 문제가 발생할 수 있음
                //ApplyLeave 보다 뒤로 빼서 안전하게
                player.Room = null;

                //본인한테 정보 전송
                {
                    S_LeaveGame leavePacket = new S_LeaveGame();
                    player.Session.Send(leavePacket);
                }
            }
            else if(type == GameObjectType.Monster)
            {
                Monster monster = null;
                if (_monsters.Remove(objectId, out monster) == false)
                    return;
                
                Map.ApplyLeave(monster);
                monster.Room = null;
            }
            else if (type == GameObjectType.Projectile)
            {
                Projectile projectile = null;
                if (_projectiles.Remove(objectId, out projectile) == false)
                    return;

                projectile.Room = null; 
            }
                
            //타인한테 정보 전송
            {
                S_Despawn despawnPacket = new S_Despawn();
                despawnPacket.ObjectIds.Add(objectId);
                foreach (Player p in _players.Values)
                {
                    if (p.Id != objectId)
                        p.Session.Send(despawnPacket);
                }
            }
            
        }

        public void HandleMove(Player player, C_Move movePacket)
        {
            if (player == null)
                return;


            //검증

            //일단 서버에서 죄표 이동
            PositionInfo movePosInfo = movePacket.PosInfo;
            ObjectInfo info = player.Info;

            //다른 죄표로 이동할 경우, 갈수 있는지 체크
            if(movePosInfo.PosX != info.PosInfo.PosX || movePosInfo.PosY != info.PosInfo.PosY)
            {
                //갈 수 없으면 block
                if (Map.CanGo(new Vector2Int(movePosInfo.PosX, movePosInfo.PosY)) == false)
                    return;
            }

            info.PosInfo.State = movePosInfo.State;
            info.PosInfo.MoveDir = movePosInfo.MoveDir;
            Map.ApplyMove(player, new Vector2Int(movePosInfo.PosX, movePosInfo.PosY));

            //다른 플레이어에게 알려주기
            S_Move resMovePacket = new S_Move();
            resMovePacket.ObjectId = player.Info.ObjectId;
            resMovePacket.PosInfo = movePacket.PosInfo;

            Broadcast(resMovePacket);
            
        }

        public void HandleSkill(Player player, C_Skill skillPacket)
        {
            if (player == null)
                return;

            ObjectInfo info = player.Info;
            if (info.PosInfo.State != CreatureState.Idle)
                return;

            //스킬 사용 가능 여부 체크
            info.PosInfo.State = CreatureState.Skill;

            S_Skill skill = new S_Skill() { Info = new SkillInfo() };
            skill.ObjectId = info.ObjectId;
            skill.Info.SkillId = skillPacket.Info.SkillId;
            Broadcast(skill);

            Data.Skill skillData = null;
            //스킬 없으면 리턴
            if (DataManager.SkillDict.TryGetValue(skillPacket.Info.SkillId, out skillData) == false)
                return;

            switch(skillData.skilltype)
            {
                case SkillType.SkillAuto:
                    {
                        //데미지 판정
                        Vector2Int skillPos = player.GetFrontCellPos(info.PosInfo.MoveDir);
                        GameObject target = Map.Find(skillPos);
                        if (target != null)
                        {
                            Console.WriteLine("Hit GameObject!");
                        }
                    }                      
                    break;
                case SkillType.SkillProjectile:
                    {
                        //Arrow
                        Arrow arrow = ObjectManager.Instance.Add<Arrow>();
                        if (arrow == null)
                            return;

                        arrow.Owner = player;
                        arrow.Data = skillData;

                        arrow.PosInfo.State = CreatureState.Moving;
                        arrow.PosInfo.MoveDir = player.PosInfo.MoveDir;
                        arrow.PosInfo.PosX = player.PosInfo.PosX;
                        arrow.PosInfo.PosY = player.PosInfo.PosY;
                        arrow.Speed = skillData.projectile.speed;
                        Push(EnterGame, arrow);
                    }                        
                    break;
            }
            
        }

        public Player FindPlayer(Func<GameObject, bool> condition)
        {
            foreach(Player player in _players.Values)
            {
                if (condition.Invoke(player))
                    return player;
            }

            return null;
        }

        public void Broadcast(IMessage packet)
        {
            foreach(Player p in _players.Values)
            {
                p.Session.Send(packet);
            }
        }

    }
}
