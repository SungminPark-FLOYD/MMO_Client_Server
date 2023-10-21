using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.DB;
using Server.Game;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

class PacketHandler
{
	public static void C_MoveHandler(PacketSession session, IMessage packet)
	{
		C_Move movePacket = packet as C_Move;
		ClientSession clientSession = session as ClientSession;

        //Console.WriteLine($"C_Move {movePacket.PosInfo.PosX}, {movePacket.PosInfo.PosY}");

		//멀티 쓰레드 환경에서 한번 체크해도 맞다는 보장이 없다
		//한번 꺼내서 null체크를 안전하게 만든다
		Player player = clientSession.MyPlayer;		
		if (player == null)
			return;

		//마찬가지로 한번 정보를 가저온 다음 null체크를 해준다
		GameRoom room = player.Room;
		if (room == null)
			return;

		room.Push(room.HandleMove, player, movePacket);		
	}

	public static void C_SkillHandler(PacketSession session, IMessage packet)
    {
		C_Skill skillPacket = packet as C_Skill;
		ClientSession clientSession = session as ClientSession;

		//멀티 쓰레드 환경에서 한번 체크해도 맞다는 보장이 없다
		//한번 꺼내서 null체크를 안전하게 만든다
		Player player = clientSession.MyPlayer;
		if (player == null)
			return;

		//마찬가지로 한번 정보를 가저온 다음 null체크를 해준다
		GameRoom room = player.Room;
		if (room == null)
			return;

		room.Push(room.HandleSkill, player, skillPacket);
	}

	public static void C_LoginHandler(PacketSession session, IMessage packet)
	{
		C_Login loginPacket = packet as C_Login;
		ClientSession clientSession = session as ClientSession;
		clientSession.HandlerLogin(loginPacket);
	}

	public static void C_EnterGameHandler(PacketSession session, IMessage packet)
    {
		C_EnterGame enterGamePacket = (C_EnterGame)packet;
		ClientSession clientSession = (ClientSession)session;
		clientSession.HandleEnterGame(enterGamePacket);
	}

	public static void C_CreatePlayerHandler(PacketSession session, IMessage packet)
	{
		C_CreatePlayer createPlayerPacket = (C_CreatePlayer)packet;
		ClientSession clientSession = (ClientSession)session;
		clientSession.HandleCreatePlayer(createPlayerPacket);
	}
}
