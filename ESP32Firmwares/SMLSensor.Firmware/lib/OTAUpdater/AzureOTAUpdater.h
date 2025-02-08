#ifndef AZUREOTAUPDATER_H
#define AZUREOTAUPDATER_H

class AzureOTAUpdater {
public:
    static bool UpdateFirmwareFromUrl(const char* firmwareUrl);
    static bool CheckUpdateStatus();
};

#endif // OTAUPDATER_H
