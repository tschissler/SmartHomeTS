#ifndef SMLPARSER_H
#define SMLPARSER_H

#include <Arduino.h>
#include <vector>
#include <stdexcept>
#include <memory>
#include <algorithm>
#include <string>

class ISMLNode {
public:
    virtual ~ISMLNode() = default;
    virtual int getType() const = 0;
};

class SMLElement : public ISMLNode {
public:
    std::vector<uint8_t> data;
    SMLElement(std::vector<uint8_t> d);
    int getType() const override { return 1; }
};

class SMLList : public ISMLNode {
public:
    std::vector<std::shared_ptr<ISMLNode>> elements;
    SMLList(std::vector<std::shared_ptr<ISMLNode>> e);
    int getType() const override { return 2; }
};

class SMLData {
public:
    // Updated to match C# implementation
    std::string ManufacturerId;
    std::string DeviceId;
    float ConsumptionEnergyTotal = 0;
    float ConsumptionEnergy1 = 0;
    float ConsumptionEnergy2 = 0;
    float FeedEnergyTotal = 0;
    float FeedEnergy1 = 0;
    float FeedEnergy2 = 0;
    float EffectivePower = 0;
    
    SMLData() = default;
    SMLData(float t1, float t2, float p);
};

class SMLParser {
public:
    static std::shared_ptr<SMLData> Parse(std::vector<uint8_t>& data);
    static bool VerifyCRC16(const std::vector<uint8_t>& buffer);
 
private:
    static int SMLElementToInteger(std::shared_ptr<SMLElement> byteData);
    static std::string SMLElementToString(std::shared_ptr<SMLElement> element);
    static bool isLittleEndian();
    static std::vector<uint8_t> ExtractPackage(std::vector<uint8_t>& data);
    static std::vector<std::shared_ptr<ISMLNode>> ExtractNodes(std::vector<uint8_t>& data);
    static std::vector<std::shared_ptr<ISMLNode>> ExtractNodes(std::vector<uint8_t>& data, int& index, int listitems);
    static uint16_t ComputeCRC16(const std::vector<uint8_t>& data, size_t offset, size_t length);
    static bool CompareArrays(const std::vector<uint8_t>& array1, const std::vector<uint8_t>& array2);
};

#endif // SMLPARSER_H
