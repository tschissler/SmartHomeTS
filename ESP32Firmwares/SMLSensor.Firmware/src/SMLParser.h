#ifndef SMLPARSER_H
#define SMLPARSER_H

#include <Arduino.h>
#include <vector>
#include <stdexcept>
#include <memory>
#include <algorithm>
#include <optional>

// Enum für die Knotentypen, um magische Zahlen zu vermeiden
enum class SMLNodeType {
    Element = 1,
    List = 2
};

class ISMLNode {
public:
    virtual ~ISMLNode() = default;
    virtual SMLNodeType getType() const = 0;
};

class SMLElement : public ISMLNode {
public:
    std::vector<uint8_t> data;
    SMLElement(std::vector<uint8_t> d);
    SMLNodeType getType() const override { return SMLNodeType::Element; }
};

class SMLList : public ISMLNode {
public:
    std::vector<std::shared_ptr<ISMLNode>> elements;
    SMLList(std::vector<std::shared_ptr<ISMLNode>> e);
    SMLNodeType getType() const override { return SMLNodeType::List; }
};

class SMLData {
public:
    std::optional<float> Tarif1;
    std::optional<float> Tarif2;
    std::optional<float> Power;
};

class SMLParser {
public:
    static std::shared_ptr<SMLData> Parse(std::vector<uint8_t>& data);
    static bool VerifyCRC16(const std::vector<uint8_t>& buffer);
 
private:
    static int SMLElementToInteger(std::shared_ptr<SMLElement> byteData);
    static bool isLittleEndian();
    static std::vector<uint8_t> ExtractPackage(std::vector<uint8_t>& data);
    static std::vector<std::shared_ptr<ISMLNode>> ExtractNodes(std::vector<uint8_t>& data);
    static std::vector<std::shared_ptr<ISMLNode>> ExtractNodes(std::vector<uint8_t>& data, int& index, int listitems);
    static uint16_t ComputeCRC16(const std::vector<uint8_t>& data, size_t offset, size_t length);
};

#endif // SMLPARSER_H
