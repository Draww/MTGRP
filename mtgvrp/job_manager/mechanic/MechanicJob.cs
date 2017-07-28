﻿using System;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Server.Managers;
using mtgvrp.player_manager;
using mtgvrp.vehicle_manager;
using mtgvrp.inventory;
using mtgvrp.core;
using mtgvrp.core.Help;
using mtgvrp.core.Items;
using System.Linq;
using GrandTheftMultiplayer.Server;

namespace mtgvrp.job_manager.taxi
{
    public class MechanicJob : Script
    {

        [Command("fixcar"), Help(HelpManager.CommandGroups.MechanicJob, "Used to fix the car you're inside.")]
        public void fixcar_cmd(Client player)
        {
            Character character = player.GetCharacter();
            var veh = VehicleManager.GetVehFromNetHandle(API.getPlayerVehicle(player));


            if (character?.JobOne?.Type != JobManager.JobTypes.Mechanic)
            {
                API.sendPictureNotificationToPlayer(player, "You must be a mechanic to use this command.", "CHAR_BLOCKED", 0, 0, "Server", "~r~Command Error");
                return;
            }

            if (veh == null)
            {
                API.sendChatMessageToPlayer(player, "You must be inside of the vehicle to fix it.");
                return;
            }

            if (DateTime.Now < character.FixcarPrevention)
            {
                API.sendChatMessageToPlayer(player, "You must wait 2 minutes before fixing another car.");
                return;
            }

            
            if (InventoryManager.DoesInventoryHaveItem(character, typeof(EngineParts)).Length == 0)
            {
                player.sendChatMessage("You don't have enough engine parts.");
                return;
            }

            API.setVehicleHealth(veh.NetHandle, 1000);
            API.repairVehicle(veh.NetHandle);
            InventoryManager.DeleteInventoryItem(character, typeof(EngineParts), 1);
            player.sendChatMessage("Vehicle repaired.");
            LogManager.Log(LogManager.LogTypes.Stats, $"[Vehicle] {character.CharacterName}[{player.GetAccount().AccountName}] has fixed vehicle #{veh.Id}.");
            character.FixcarPrevention = DateTime.Now.Add(TimeSpan.FromSeconds(10));
        }

        [Command("paintcar"), Help(HelpManager.CommandGroups.MechanicJob, "Used to paint the car you're inside. <br/> Use the wiki to get the color ids.", "The primary color.", "The secondary color.")]
        public void paintcar_cmd(Client player, int col1, int col2)
        {
            Character character = player.GetCharacter();
            var veh = VehicleManager.GetVehFromNetHandle(API.getPlayerVehicle(player));

            if (character?.JobOne?.Type != JobManager.JobTypes.Mechanic)
            {
                API.sendPictureNotificationToPlayer(player, "You must be a mechanic to use this command.", "CHAR_BLOCKED", 0, 0, "Server", "~r~Command Error");
                return;
            }

            if (InventoryManager.DoesInventoryHaveItem(character, typeof(SprayPaint)).Length == 0)
            {
                player.sendChatMessage("You don't have spray paint.");
                return;
            }

            API.setVehiclePrimaryColor(API.getPlayerVehicle(player), col1);
            API.setVehicleSecondaryColor(API.getPlayerVehicle(player), col2);
            veh.SpawnColors[0] = col1;
            veh.SpawnColors[1] = col2;
            veh.Save();
            InventoryManager.DeleteInventoryItem(character, typeof(SprayPaint), 1);
            player.sendChatMessage("Vehicle painted.");

            LogManager.Log(LogManager.LogTypes.Stats, $"[Vehicle] {character.CharacterName}[{player.GetAccount().AccountName}] has painted vehicle #{veh.Id}.");

            var tst = Enum.GetValues(typeof(HelpManager.CommandGroups)).Cast<HelpManager.CommandGroups>().Where(x => x > HelpManager.CommandGroups.AdminLevel1);
        }
   
    }

}
