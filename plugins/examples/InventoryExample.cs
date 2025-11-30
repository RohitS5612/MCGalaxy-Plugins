// This is a basic example of how to use databases in MCGalaxy. This is NOT a survival plugin.
using System.Collections.Generic;
using MCGalaxy;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Network;
using MCGalaxy.SQL;
using BlockID = System.UInt16;

namespace Core
{
    public class InventoryExample : Plugin
    {
        public override string name { get { return "InventoryExample"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.3"; } }
        public override string creator { get { return "Venk"; } }

        public override void Load(bool startup)
        {
            Database.CreateTable("Inventories", inventoriesTable); // Create the table to ensure it exists before we try and get data from it
            Command.Register(new CmdInventory());
            OnPlayerClickEvent.Register(HandlePlayerClick, Priority.Low);
        }

        public override void Unload(bool shutdown)
        {
            Command.Unregister(Command.Find("Inventory"));
            OnPlayerClickEvent.Unregister(HandlePlayerClick);
        }

        private ColumnDesc[] inventoriesTable = new ColumnDesc[] {
            new ColumnDesc("PlayerName", ColumnType.VarChar, 20),
            new ColumnDesc("Slot", ColumnType.Int32),
            new ColumnDesc("BlockID", ColumnType.UInt16),
            new ColumnDesc("Quantity", ColumnType.Int32),
            new ColumnDesc("Metadata", ColumnType.VarChar, 100)
        };

        /// <summary>
        /// This is a super basic example of just clicking on blocks to add/remove them from the inventory. You might wish to use your own system.
        /// </summary>
        void HandlePlayerClick(Player p, MouseButton button, MouseAction action, ushort yaw, ushort pitch, byte entity, ushort x, ushort y, ushort z, TargetBlockFace face)
        {
            if (action != MouseAction.Pressed) return;

            BlockID clickedBlock = p.level.GetBlock(x, y, z);
            if (clickedBlock == Block.Air || clickedBlock == Block.Invalid) return;

            // Breaking
            if (button == MouseButton.Left)
                AddItemToInventory(p, clickedBlock, 1);

            // Placing
            if (button == MouseButton.Right)
                RemoveItemFromInventory(p, p.GetHeldBlock(), 1);
        }

        public static bool AddItemToInventory(Player p, BlockID block, int quantity = 1)
        {
            int id = block;
            if (id >= 66) id -= 256; // Need to convert block if ID is over 66

            // The '*' means all data, and the 'WHERE PlayerName=@' means it will only show data for the specified player
            List<string[]> rows = Database.GetRows("Inventories", "*", "WHERE PlayerName=@0", p.truename); 

            Dictionary<int, string[]> slotData = new Dictionary<int, string[]>();

            foreach (string[] row in rows)
            {
                int slot;
                if (int.TryParse(row[1], out slot))
                {
                    slotData[slot] = row;

                    ushort existingBlockID = (ushort)int.Parse(row[2]);
                    int existingQuantity = int.Parse(row[3]);

                    if (existingBlockID == id)
                    {
                        int newQuantity = existingQuantity + quantity;

                        Database.UpdateRows(
                            "Inventories",
                            "Quantity=@0",
                            "WHERE PlayerName=@1 AND Slot=@2",
                            newQuantity, p.truename, slot
                        );

                        p.Message("&anew: " + newQuantity);

                        return true;
                    }
                }
            }

            for (int i = 1; i <= 30; i++)
            {
                if (!slotData.ContainsKey(i))
                {
                    Database.AddRow("Inventories", "PlayerName, Slot, BlockID, Quantity", p.truename, i, id, quantity);
                    p.Message("&anew: " + quantity);
                    CmdInventory.UpdateBlockMenu(p); // Optional: use the block menu for displaying the player's inventory

                    // You can force the player to hold the new block in their hand by doing this:
                    if (p.Session.SendHoldThis(block, false))
                        return true;

                    return true;
                }
            }

            p.Message("&cYour inventory is full.");
            return false;
        }

        /// <summary>
        /// This is an example method to show metadata. You probably won't need this if you are just using a basic inventory; however, for
        /// more complex systems, this can be used for level detection, durability, ownership of items etc.
        /// </summary>
        public static bool HasItemWithLevel(Player p, ushort blockID, int requiredLevel)
        {
            int id = blockID;
            if (id >= 66) id -= 256; // Need to convert block if ID is over 66

            List<string[]> rows = Database.GetRows("Inventories", "*", "WHERE PlayerName=@0 AND BlockID=@1", p.truename, id);

            foreach (var row in rows)
            {
                string metadata = row.Length >= 5 ? row[4] : null;

                if (metadata != null && metadata.Contains("level="))
                {
                    string[] parts = metadata.Split('=');
                    int level;
                    if (parts.Length == 2 && int.TryParse(parts[1], out level))
                    {
                        if (level >= requiredLevel)
                            return true;
                    }
                }
            }

            return false;
        }

        public static bool RemoveItemFromInventory(Player p, ushort blockID, int quantity = 1)
        {
            int id = blockID;
            if (id >= 66) id -= 256; // Need to convert block if ID is over 66

            List<string[]> rows = Database.GetRows("Inventories", "*", "WHERE PlayerName=@0", p.truename);
            if (rows.Count == 0)
            {
                // p.Message("&cYou do not have anything in your inventory.");
                return false;
            }

            bool itemFound = false;

            foreach (string[] row in rows)
            {
                int slot;
                if (int.TryParse(row[1], out slot))
                {
                    ushort existingBlockID = (ushort)int.Parse(row[2]);
                    int existingQuantity = int.Parse(row[3]);

                    if (existingBlockID == id)
                    {
                        itemFound = true;

                        if (existingQuantity >= quantity)
                        {
                            int newQuantity = existingQuantity - quantity;

                            if (newQuantity > 0)
                            {
                                // Update the new amount in the database
                                Database.UpdateRows(
                                    "Inventories",
                                    "Quantity=@0",
                                    "WHERE PlayerName=@1 AND Slot=@2",
                                    newQuantity, p.truename, slot
                                );

                                p.Message("&cnew: " + newQuantity);
                            }
                            else
                            {
                                Database.DeleteRows("Inventories", "WHERE PlayerName=@0 AND Slot=@1", p.truename, slot);
                                p.Message("&anew: 0");
                                CmdInventory.UpdateBlockMenu(p);
                                if (p.Session.SendHoldThis(Block.Air, false)) return true;
                            }

                            return true;
                        }

                        else
                        {
                            p.Message("&cYou do not have enough of block ID " + id + " to remove.");
                            return false;
                        }
                    }
                }
            }

            return itemFound;
        }

        public static int GetItemQuantity(Player p, ushort blockID)
        {
            int id = blockID;
            if (id >= 66) id -= 256;
            List<string[]> rows = Database.GetRows("Inventories", "*", "WHERE PlayerName=@0 AND BlockID=@1", p.truename, id);
            if (rows.Count == 0)
            {
                // p.Message("&cYou do not have anything in your inventory.");
                return 0;
            }
            int count = 0;
            foreach (var row in rows)
                count += int.Parse(row[3]);

            return count;
        }

        public static bool Has(Player p, ushort blockId)
        {
            return GetItemQuantity(p, blockId) > 0;
        }
    }

    public class CmdInventory : Command2
    {
        public override string name { get { return "Inventory"; } }
        public override string type { get { return "information"; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }

        public override void Use(Player p, string message)
        {
            UpdateBlockMenu(p, true);
        }

        public override void Help(Player p)
        {
            p.Message("&T/Inventory &H- Opens the inventory.");
        }

        /// <summary>
        /// This is a cool trick I added into the client for utilising the player's block inventory as a pseudo 'GUI'. It works by
        /// clearing the default block list, then re-ordering it to match our inventory using the 'InventoryOrder' packet.
        /// </summary>
        public static void UpdateBlockMenu(Player p, bool open = false)
        {
            // Clear the existing block menu so we can show inventory blocks instead
            for (int i = 0; i <= 767; i++)
            {
                p.Send(Packet.SetInventoryOrder(Block.Air, (BlockID)i, p.Session.hasExtBlocks));
            }

            List<string[]> rows = Database.GetRows("Inventories", "*", "WHERE PlayerName=@0", p.truename);
            foreach (string[] row in rows)
            {
                int slot;
                ushort block;

                if (int.TryParse(row[1], out slot) && ushort.TryParse(row[2], out block))
                {
                    if (slot >= 1 && slot <= 30)
                        p.Send(Packet.SetInventoryOrder(block, (BlockID)slot, p.Session.hasExtBlocks));
                }
            }

            if (open) p.Send(Packet.ToggleBlockList(false)); // You can force players to open the inventory with this
        }
    }
}