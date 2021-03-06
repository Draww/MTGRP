using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

using GTANetworkAPI;

using mtgvrp.core;
using mtgvrp.inventory;
using mtgvrp.player_manager;
using mtgvrp.core.Help;

namespace mtgvrp.vehicle_manager
{
    class VehicleOwnership : Script
    {
        public VehicleOwnership()
        {
        }

        [RemoteEvent("myvehicles_locatecar")]
        public void MyVehiclesLocateCar(Player sender, params object[] arguments)
        {
            Character character = sender.GetCharacter();
            GameVehicle lcVeh =
                        VehicleManager.Vehicles.Single(
                            x => x.Entity.Value == Convert.ToInt32(arguments[0]) && x.OwnerId == character.Id);
            Vector3 loc = NAPI.Entity.GetEntityPosition(lcVeh.Entity);
            NAPI.ClientEvent.TriggerClientEvent(sender, "myvehicles_setCheckpointToCar", loc.X, loc.Y, loc.Z);
            NAPI.Chat.SendChatMessageToPlayer(sender, "A checkpoint has been set to the vehicle.");
        }

        [RemoteEvent("myvehicles_abandoncar")]
        public void MyVehiclesAbandonCar(Player sender, params object[] arguments)
        {
            Character character = sender.GetCharacter();
            GameVehicle acVeh =
                        VehicleManager.Vehicles.Single(
                            x => x.Id == Convert.ToInt32(arguments[0]) && x.OwnerId == character.Id);
            VehicleManager.despawn_vehicle(acVeh);
            VehicleManager.delete_vehicle(acVeh);
            acVeh.Delete();
            NAPI.Chat.SendChatMessageToPlayer(sender,
                $"You have sucessfully abandoned your ~r~{returnCorrDisplayName(acVeh.VehModel)}~w~");
        }

        [RemoteEvent("myvehicles_sellcar")]
        public void MyVehiclesSellCar(Player sender, params object[] arguments)
        {
            Character character = sender.GetCharacter();
            GameVehicle scVeh =
                        VehicleManager.Vehicles.Single(
                            x => x.Id == Convert.ToInt32(arguments[0]) && x.OwnerId == character.Id);
            var tid = (string)arguments[1];
            var target = PlayerManager.ParseClient(tid);
            if (target == null)
            {
                NAPI.Chat.SendChatMessageToPlayer(sender, "That player isn't online or doesn't exist.");
                return;
            }
            var targetChar = target.GetCharacter();
            var targetAccount = target.GetAccount();

            var price = 0;
            if (!int.TryParse((string)arguments[2], out price))
            {
                NAPI.Chat.SendChatMessageToPlayer(sender, "Invalid price entered.");
                return;
            }
            if (price < 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(sender, "Price cannot be negative.");
                return;
            }

            if (targetChar.OwnedVehicles.Count >= VehicleManager.GetMaxOwnedVehicles(targetChar.Player))
            {
                NAPI.Chat.SendChatMessageToPlayer(sender, "This player cannot own any more vehicles.");
                return;
            }

            NAPI.Chat.SendChatMessageToPlayer(sender,
                $"Are you sure you would like to sell the ~r~{returnCorrDisplayName(scVeh.VehModel)}~w~ for ~r~${price}~w~ to the player ~r~{targetChar.rp_name()}~w~?");
            NAPI.Chat.SendChatMessageToPlayer(sender, "Use /confirmsellvehicle to sell.");
            NAPI.Data.SetEntityData(sender, "sellcar_selling", new dynamic[] { scVeh, targetChar, price });
        }

        [RemoteEvent("groupvehicles_locatecar")]
        public void GroupVehiclesLocateCar(Player sender, params object[] arguments)
        {
            Character character = sender.GetCharacter();
            GameVehicle gVeh =
                        VehicleManager.Vehicles.Single(
                            x => x.Entity.Value == Convert.ToInt32(arguments[0]) && x.GroupId == character.GroupId);
            Vector3 location = NAPI.Entity.GetEntityPosition(gVeh.Entity);
            NAPI.ClientEvent.TriggerClientEvent(sender, "myvehicles_setCheckpointToCar", location.X, location.Y, location.Z);
            NAPI.Chat.SendChatMessageToPlayer(sender, "A checkpoint has been set to the vehicle.");
        }

        [Command("myvehicles"), Help(HelpManager.CommandGroups.Vehicles, "Lists the vehicles you own.", null)]
        public void myvehicles_cmd(Player player)
        {
            //Get all owned vehicles and send them.
            Character character = player.GetCharacter();
            if (!character.OwnedVehicles.Any())
            {
                NAPI.Chat.SendChatMessageToPlayer(player,"You don't have any vehicles to manage!");
                return;
            }
            string[][] cars = character.OwnedVehicles
                .Select(x => new[]
                    {returnCorrDisplayName(x.VehModel), x.Id.ToString(), x.Entity.Value.ToString()})
                .ToArray();

            NAPI.ClientEvent.TriggerClientEvent(player, "myvehicles_showmenu", NAPI.Util.ToJson(cars));
        }

        [Command("confirmsellvehicle"),
         Help(HelpManager.CommandGroups.Vehicles, "To confirm that you want to sell your vehicle.", null)]
        public void confirmsellvehicle_cmd(Player player)
        {
            Character character = player.GetCharacter();
            var data = NAPI.Data.GetEntityData(player, "sellcar_selling");
            if (data != null)
            {
                GameVehicle veh = data[0];
                Character target = data[1];
                int price = data[2];
                NAPI.Data.SetEntityData(target.Player, "sellcar_buying", new dynamic[] {character, veh, price});
                NAPI.Data.SetEntityData(player, "sellcar_selling", null);
                NAPI.Chat.SendChatMessageToPlayer(target.Player,
                    $"~r~{character.rp_name()}~w~ has offered to sell you a ~r~{returnCorrDisplayName(veh.VehModel)}~w~ for ~r~${price}~w~.");
                NAPI.Chat.SendChatMessageToPlayer(target.Player, "Use /confirmbuyvehicle to buy it.");
                NAPI.Chat.SendChatMessageToPlayer(player, "Request sent.");
            }
            else
                NAPI.Chat.SendChatMessageToPlayer(player, "You aren't selling any car.");
        }

        [Command("confirmbuyvehicle"),
         Help(HelpManager.CommandGroups.Vehicles, "To confirm that you want to buy a vehicle.", null)]
        public void confirmbuyvehicle_cmd(Player player)
        {
            Character character = player.GetCharacter();
            Account account = player.GetAccount();
            var data = NAPI.Data.GetEntityData(player, "sellcar_buying");
            if (data != null)
            {
                Character buyingFrom = data[0];
                GameVehicle veh = data[1];
                int price = data[2];
                //Make sure near him.
                var buyingPos = buyingFrom.Player.Position;
                if (player.Position.DistanceTo(buyingPos) <= 5f)
                {
                    //make sure still have slots.
                    if (character.OwnedVehicles.Count < VehicleManager.GetMaxOwnedVehicles(character.Player))
                    {
                        //make sure have money.
                        if (Money.GetCharacterMoney(character) >= price)
                        {
                            //Do actual process.
                            InventoryManager.GiveInventoryItem(buyingFrom, new Money(), price);
                            InventoryManager.DeleteInventoryItem(character, typeof(Money), price);
                            veh.OwnerId = character.Id;
                            veh.OwnerName = character.CharacterName;
                            veh.Save();

                            //DONE, now spawn if hes vip.
                            if (!veh.IsSpawned)
                            {
                                //He won't be able to buy it anyways if he wasn't VIP... so I THINK he can now have it spawned, right ? :/
                                if (VehicleManager.spawn_vehicle(veh) != 1)
                                    NAPI.Util.ConsoleOutput(
                                        $"There was an error spawning vehicle #{veh.Id} of {character.CharacterName}.");
                            }

                            //Tell.
                            NAPI.Chat.SendChatMessageToPlayer(player, "You have sucessfully bought the car.");
                            NAPI.Chat.SendChatMessageToPlayer(buyingFrom.Player, "You have successfully sold the car.");
                            NAPI.Data.SetEntityData(player, "sellcar_buying", null);
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, "You don't have enough money.");
                            NAPI.Chat.SendChatMessageToPlayer(buyingFrom.Player, "The buyer doesn't have enough money.");
                            NAPI.Data.SetEntityData(player, "sellcar_buying", null);
                        }
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, "You can't own anymore vehicles.");
                        NAPI.Chat.SendChatMessageToPlayer(buyingFrom.Player, "The buyer can't own anymore vehicles.");
                        NAPI.Data.SetEntityData(player, "sellcar_buying", null);
                    }
                }
                else
                {
                    NAPI.Chat.SendChatMessageToPlayer(player, "You must be near the buyer.");
                    NAPI.Chat.SendChatMessageToPlayer(buyingFrom.Player, "The buyer must be near you.");
                    NAPI.Data.SetEntityData(player, "sellcar_buying", null);
                }
            }
            else
                NAPI.Chat.SendChatMessageToPlayer(player, "You aren't buying any car.");
        }

        public static string returnCorrDisplayName(VehicleHash hash)
        {
            string disName = API.Shared.GetVehicleDisplayName(hash);
            if (disName != null)
            {
                return disName;
            }
            switch(hash)
            {
                case VehicleHash.Ratbike:
                    return "Ratbike";
                case VehicleHash.Chimera:
                    return "Chimera";
                case VehicleHash.Zombiea:
                    return "Zombie";
                case VehicleHash.Faggio:
                    return "Faggio Sport";
                case VehicleHash.Avarus:
                    return "Avarus";
                case VehicleHash.Sanctus:
                    return "Santus";
                case VehicleHash.Elegy:
                    return "Elegy";
                case VehicleHash.Sultanrs:
                    return "Sultan RS";
                case VehicleHash.Zentorno:
                    return "Zentorno";
                case VehicleHash.Turismo2:
                    return "Turismo";
                case VehicleHash.Italigtb2:
                    return "Itali GTB";
                case VehicleHash.Nero:
                    return "Nero";
                default:
                    return "Vehicle";
            }
        }



    }

}
