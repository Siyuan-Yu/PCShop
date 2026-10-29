using System;

namespace PCShopSell.Core.Session
{
    /// <summary>存档格式。字段为公开小写，可直接用 Unity JsonUtility 序列化。出行中、店里的顾客不存。</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public string savedAt;
        /// <summary>随机数状态（ulong 的十进制字符串，避免 JSON 精度问题）。</summary>
        public string rngState;
        public int day;
        public int minute;

        public long balanceCents;
        public SaveLedgerEntry[] ledger;

        public double[] marketFactors;
        public SaveNews[] news;
        public SaveHistory[] history;

        public string playerNode;
        public string currentPlace;
        public int vehicle;
        public int[] ownedVehicles;
        public bool tricycleImpounded;
        public SaveStockLine[] cargo;

        public double hunger, energy, health, sleepDebt;
        public int intelligence, charm, skill;
        public int lastNewsReadDay;

        public SaveStockLine[] shopStock;
        public double markup;
        public string[] partMarkupIds;
        public double[] partMarkupValues;
        public string[] restockIds;
        public int[] restockTargets;
        public string restockStore;
        public double reputation;
        public int accepted;
        public SaveOrder[] orders;
        public int nextOrderId;
        public int[] displayPcs;

        public string[] discoveredStores;
        public SaveStoreRecord[] storeRecords;
        public SaveOnlineOrder[] onlineOrders;
        public int nextOnlineOrderId;
        public int totalOnlineOrders;

        public SaveBargain[] bargains;

        public SaveEmployee employee;
        public bool hasEmployee;
        public bool everHired;
        public SaveEmployee[] candidates;
        public int jobAdDay;

        public bool monthGoalEvaluated;
        public bool monthGoalPassed;
        public int negativeCashDays;
        public bool gameOver;
        public string gameOverReason;

        public string[] upgrades;
        public string[] outfits;
        public string outfitEquipped;
        public int homeComfort;
        public SaveHome home;
        /// <summary>没有智能手机（旧存档没有这个字段，视为有）。</summary>
        public bool noPhone;
        /// <summary>手机里装的 app（旧存档没有这个字段，视为全装了）。</summary>
        public string[] apps;
        public string[] flags;
        public int[] scheduledDays;
        public long[] scheduledCents;
        public string[] scheduledMemos;
        public string[] eventIds;
        public int[] eventLastDays;
        public int[] eventCounts;

        // 长线目标 / 人脉 / 扩建
        public string[] milestoneIds;
        public bool[] milestonePassed;
        public int rentHikes;
        public string endingId;
        public int shopSize;

        // 租铺子：0 = 旧存档（按开局那间店、按店面大小算月租），1 = 租着铺子，2 = 还没租
        public int shopState;
        public string leasePlace;
        public long leaseRentCents;
        public long leaseDepositCents;
        public int leaseStartDay;
        public SaveListing[] listings;
        public string[] vacantPlaces;
        public string realtyRng;

        public string[] relationIds;
        public double[] relationValues;
        public string[] giftIds;
        public int[] giftDays;
        public string[] chatIds;
        public int[] chatDays;
        public string[] talkIds;
        public int[] talkDays;
        public string[] lifeIds;
        public int[] lifeCounts;
        public string[] lifeDayIds;
        public int[] lifeDays;
        public string[] insiderIds;
        public int[] insiderDays;
        public string[] staffIds;
        public int[] staffDays;
        public string[] rapIds;
        public double[] rapValues;
        public string[] rapChatIds;
        public int[] rapChatDays;
        public string[] rapGiftIds;
        public int[] rapGiftDays;
        public string[] rapStoryIds;
        public int[] rapStoryCounts;
        public string[] rapStoryDayIds;
        public int[] rapStoryDays;
        public SaveSocial social;
    }

    /// <summary>档口老板、约会、行情消息相关的存档。</summary>
    [Serializable]
    public sealed class SaveSocial
    {
        public string[] apptNpc;
        public int[] apptSlot;
        public int[] apptDay;

        public string[] tipNpc;
        public int[] tipCategory;
        public int[] tipDirection;
        public int[] tipIssued;
        public int[] tipResolve;
        public double[] tipIndex;
        public int[] tipOutcome;

        public string[] outingIds;
        public int[] outingDays;
        public string[] inviteTryIds;
        public int[] inviteTryDays;
        public string[] tipDayIds;
        public int[] tipDays;
        public string[] scoreIds;
        public int[] scoreRight;
        public int[] scoreWrong;
        public string[] boughtKeys;
        public int[] boughtDays;
        public int[] boughtCounts;

        public double[] marketBias;
        public int[] shockDays;
        public int[] shockCategories;
        public double[] shockDeltas;
    }

    [Serializable]
    public sealed class SaveLedgerEntry
    {
        public int day;
        public string category;
        public long cents;
        public string memo;
        public long balanceAfter;
    }

    [Serializable]
    public sealed class SaveNews
    {
        public int day;
        public string headline;
        public int factor;
        public double shock;
    }

    [Serializable]
    public sealed class SaveHistory
    {
        public int category;
        public double[] values;
    }

    [Serializable]
    public sealed class SaveStockLine
    {
        public string part;
        public bool boxed;
        public int count;
    }

    [Serializable]
    public sealed class SaveOrder
    {
        public int id;
        public int tier;
        public long priceCents;
        public long placedAt;
        public long dueAt;
    }

    [Serializable]
    public sealed class SaveStoreRecord
    {
        public string store;
        public int received;
        public int defects;
    }

    [Serializable]
    public sealed class SaveOnlineOrder
    {
        public int id;
        public string store;
        public long deliverAt;
        public long totalCents;
        public SaveStockLine[] lines;
    }

    [Serializable]
    public sealed class SaveBargain
    {
        public string stall;
        public int day;
        public double bonus;
    }

    [Serializable]
    public sealed class SaveEmployee
    {
        public string name;
        public long wage;
        public int checkoutMinutes;
        public string description;
    }

    [Serializable]
    public sealed class SaveHome
    {
        public bool saved;
        public string[] stockIds;
        public int[] stockCounts;
        public SavePlaced[] homeLayout;
        public SavePlaced[] shopLayout;
        public SaveParcel[] parcels;
        public double riceJin;
        public SaveBatch[] ingredients;
        public SaveBatch[] cooked;
        /// <summary>老存档：放在家里的饭盒（读档时转进背包）。</summary>
        public SaveBatch[] packed;
        public SaveBatch[] packLunch;
        public SaveCourier[] couriers;
        public bool sendByCourier;
        public int renovationDay;
        public SaveBatch[] cooking;
        public long cookReadyMinute;
        public string[] visitTargets;
        public long[] visitStarts;
        public long[] visitPrices;
        public long[] visitWorkStarts;
        public double packRice;
        public SaveBatch[] packIngredients;
        public string[] giftIds;
        public int[] giftCounts;
        public string[] boxGiftIds;
        public int[] boxGiftCounts;
        public double boxRice;
        public SaveBatch[] boxIngredients;
        public SaveBatch[] boxLunch;
        public string[] boxFurnIds;
        public int[] boxFurnCounts;
        public string[] boxPartIds;
        public bool[] boxPartBoxed;
        public int[] boxPartCounts;
        public string[] packFurnIds;
        public int[] packFurnCounts;
        public string[] mallStashIds;
        public int[] mallStashCounts;
        public SaveStorage shopLocker;

        /// <summary>叫车送到哪：0 = 按店默认，1 = 家里，2 = 店里。</summary>
        public int courierDest;
    }

    /// <summary>一个储物处（店里储物柜）里的米菜、饭盒、礼品、家具。</summary>
    [Serializable]
    public sealed class SaveStorage
    {
        public string[] giftIds;
        public int[] giftCounts;
        public double rice;
        public SaveBatch[] ingredients;
        public SaveBatch[] lunch;
        public string[] furnIds;
        public int[] furnCounts;
    }

    [Serializable]
    public sealed class SavePlaced
    {
        public string id;
        public double x;
        public double z;
        public int rot;
    }

    [Serializable]
    public sealed class SaveParcel
    {
        public int kind;
        public string id;
        public int count;
        public int dueDay;
        public bool toShop;
    }

    [Serializable]
    public sealed class SaveCourier
    {
        public string fromId;
        public string fromName;
        public bool toShop;
        public long dueMinute;
        public int[] kinds;
        public string[] ids;
        public int[] counts;
        public double[] jins;
        public bool[] boxed;
        public int[] expireDays;
    }

    [Serializable]
    public sealed class SaveListing
    {
        public string place;
        public int size;
        public long rentCents;
        public int openDay;
        public int closeDay;
    }

    [Serializable]
    public sealed class SaveBatch
    {
        public string id;
        public string name;
        public double hunger;
        public double health;
        public int count;
        public int expireDay;
    }
}
