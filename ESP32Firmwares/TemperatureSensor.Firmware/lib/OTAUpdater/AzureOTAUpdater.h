#ifndef AZUREOTAUPDATER_H
#define AZUREOTAUPDATER_H

class AzureOTAUpdater {
public:
    static bool UpdateFirmwareFromUrl(const char* firmwareUrl);
    static void CheckUpdateStatus();
};

#endif // OTAUPDATER_H
