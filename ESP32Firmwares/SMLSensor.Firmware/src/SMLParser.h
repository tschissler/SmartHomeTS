#ifndef SMLPARSER_H
#define SMLPARSER_H

#include <Arduino.h>
#include <vector>
#include <stdexcept>
#include <algorithm>

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
    std::vector<ISMLNode*> elements;
    SMLList(std::vector<ISMLNode*> e);
    ~SMLList();
    int getType() const override { return 2; }
};

class SMLData {
public:
    float Tarif1;
    float Tarif2;
    float Power;
    SMLData(float t1, float t2, float p);
};

class SMLParser {
public:
    static SMLData* Parse(std::vector<uint8_t>& data);

private:
    static int SMLElementToInteger(SMLElement* byteData);
    static bool isLittleEndian();
    static std::vector<uint8_t> ExtractPackage(std::vector<uint8_t>& data);
    static std::vector<ISMLNode*> ExtractNodes(std::vector<uint8_t>& data);
    static std::vector<ISMLNode*> ExtractNodes(std::vector<uint8_t>& data, int& index, int listitems);
};

#endif // SMLPARSER_H
