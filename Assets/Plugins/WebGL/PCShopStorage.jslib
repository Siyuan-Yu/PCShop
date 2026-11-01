mergeInto(LibraryManager.library, {
  PCShop_SyncStorage: function () {
    if (Module.pcshopSyncBusy) { Module.pcshopSyncAgain = true; return; }
    function flush() {
      Module.pcshopSyncBusy = true;
      FS.syncfs(false, function (error) {
        Module.pcshopSyncBusy = false;
        if (error) console.warn("PCShop: browser save persistence failed", error);
        if (Module.pcshopSyncAgain) { Module.pcshopSyncAgain = false; flush(); }
      });
    }
    flush();
  }
});
