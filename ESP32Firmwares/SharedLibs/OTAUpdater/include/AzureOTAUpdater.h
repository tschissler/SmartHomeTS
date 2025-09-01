#ifndef AZUREOTAUPDATER_H
#define AZUREOTAUPDATER_H

class AzureOTAUpdater {
public:
    static bool UpdateFirmwareFromUrl(const char* firmwareUrl);
    static int CheckUpdateStatus();
    static String ExtractVersionFromUrl(String url);
};

#endif // OTAUPDATER_H
