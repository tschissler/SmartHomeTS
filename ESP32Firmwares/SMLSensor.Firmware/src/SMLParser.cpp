#include "SMLParser.h"

// SMLElement Constructor
SMLElement::SMLElement(std::vector<uint8_t> d) : data(d) {}

// SMLList Constructor
SMLList::SMLList(std::vector<ISMLNode*> e) : elements(e) {}

// SMLList Destructor
SMLList::~SMLList() {
    for (ISMLNode* element : elements) {
        delete element;
    }
}

// SMLData Constructor
SMLData::SMLData(float t1, float t2, float p) : Tarif1(t1), Tarif2(t2), Power(p) {}

// SMLParser Methods
SMLData* SMLParser::Parse(std::vector<uint8_t>& data) {
    std::vector<ISMLNode*> elements = ExtractNodes(data);
    
    if (elements.empty()) {
        throw std::runtime_error("Error while parsing SML package. No elements could be identified");
    }

    if (elements.size() < 2) {
        throw std::runtime_error("Error while parsing SML package. Expected 2 elements on root level, but found " + std::to_string(elements.size()));
    }

    if (elements[1] == nullptr) {
        throw std::runtime_error("Error while parsing SML package. Second element on root level is null");
    }

    if (elements[1]->getType() != 2) {
        throw std::runtime_error("Error while parsing SML package. Second element on root level is not a list");
    }

    SMLList* dataRootElement = static_cast<SMLList*>(elements[1]);

    if (dataRootElement->elements.size() < 4) {
        throw std::runtime_error("Error while parsing SML package. Second list does not contain enough elements");
    }

    if (dataRootElement->elements[3] == nullptr) {
        throw std::runtime_error("Error while parsing SML package. Fourth element on second level is null");
    }

    if (dataRootElement->elements[3]->getType() != 2) {
        throw std::runtime_error("Error while parsing SML package. Fourth element on second level is not a list");
    }

    SMLList* dataLevel2Element = static_cast<SMLList*>(dataRootElement->elements[3]);

    if (dataLevel2Element->elements.size() < 2) {
        throw std::runtime_error("Error while parsing SML package. Third list does not contain enough elements");
    }

    if (dataLevel2Element->elements[1] == nullptr ||
        ((SMLList*)dataLevel2Element->elements[1])->elements[4] == nullptr ||
        ((SMLList*)((SMLList*)dataLevel2Element->elements[1])->elements[4])->elements[2] == nullptr ||
        ((SMLList*)((SMLList*)((SMLList*)dataLevel2Element->elements[1])->elements[4])->elements[2])->elements[5] == nullptr) {
        throw std::runtime_error("Error while parsing SML package. Elements are null");
    }

    try {
        SMLElement* tarif1Element = static_cast<SMLElement*>(((SMLList*)((SMLList*)((SMLList*)dataLevel2Element->elements[1])->elements[4])->elements[2])->elements[5]);
        SMLElement* tarif2Element = static_cast<SMLElement*>(((SMLList*)((SMLList*)((SMLList*)dataLevel2Element->elements[1])->elements[4])->elements[3])->elements[5]);
        SMLElement* LeistungsElement = static_cast<SMLElement*>(((SMLList*)((SMLList*)((SMLList*)dataLevel2Element->elements[1])->elements[4])->elements[4])->elements[5]);

        int tarif1 = SMLElementToInteger(tarif1Element);
        int tarif2 = SMLElementToInteger(tarif2Element);
        int Leistung = SMLElementToInteger(LeistungsElement);

        return new SMLData(tarif1 / 10000.0f, tarif2 / 10000.0f, Leistung);

    } catch (const std::exception& ex) {
        throw std::runtime_error("Error while parsing SML package. " + std::string(ex.what()));
    }
    return nullptr;
}

int SMLParser::SMLElementToInteger(SMLElement* byteData) {
    std::vector<uint8_t> tarif1Array(byteData->data.begin() + 1, byteData->data.end());
    if (isLittleEndian()) {
        std::reverse(tarif1Array.begin(), tarif1Array.end());
    }

    if ((byteData->data[0] >> 4) == 5) {
        return *reinterpret_cast<const int16_t*>(tarif1Array.data());
    }
    return *reinterpret_cast<const int32_t*>(tarif1Array.data());
}

bool SMLParser::isLittleEndian() {
    int num = 1;
    return (*(char*)&num == 1);
}

std::vector<uint8_t> SMLParser::ExtractPackage(std::vector<uint8_t>& data) {
    std::vector<uint8_t> startSequence = {0x1B, 0x1B, 0x1B, 0x1B, 0x01, 0x01, 0x01, 0x01};
    std::vector<uint8_t> endSequencePrefix = {0x1B, 0x1B, 0x1B, 0x1B, 0x1A};
    int startIndex = -1;
    int endIndex = -1;

    // Find the start sequence
    for (size_t i = 0; i <= data.size() - startSequence.size(); ++i) {
        bool match = true;
        for (size_t j = 0; j < startSequence.size(); ++j) {
            if (data[i + j] != startSequence[j]) {
                match = false;
                break;
            }
        }
        if (match) {
            startIndex = i + startSequence.size();
            break;
        }
    }

    if (startIndex == -1) {
        return {};
    }

    // Find the end sequence
    for (size_t i = startIndex; i <= data.size() - endSequencePrefix.size() - 3; ++i) {
        bool match = true;
        for (size_t j = 0; j < endSequencePrefix.size(); ++j) {
            if (data[i + j] != endSequencePrefix[j]) {
                match = false;
                break;
            }
        }
        if (match) {
            endIndex = i;
            break;
        }
    }

    if (endIndex == -1) {
        return {}; // No end sequence found
    }

    std::vector<uint8_t> package(data.begin() + startIndex, data.begin() + endIndex);
    data.erase(data.begin(), data.begin() + endIndex + endSequencePrefix.size() + 3);
    return package;
}

std::vector<ISMLNode*> SMLParser::ExtractNodes(std::vector<uint8_t>& data) {
    int index = 0;
    return ExtractNodes(data, index, 99);
}

std::vector<ISMLNode*> SMLParser::ExtractNodes(std::vector<uint8_t>& data, int& index, int listitems) {
    std::vector<ISMLNode*> elements;
    while (index < data.size()) {
        int elementType = data[index] >> 4; // Extract the type from the start byte
        int elementLength = data[index] & 0x0F; // Extract the length from the start byte
        if (elementLength == 0)
            elementLength = 1; // If the element is 0x00, the element is 1 byte long

        if (elementType == 0x07) {
            index++;
            std::vector<ISMLNode*> element = ExtractNodes(data, index, elementLength);
            if (element.empty() || element.size() != elementLength) {
                throw std::out_of_range("Number of elements in list does not match the specified length for the list");
            }
            else {
                elements.push_back(new SMLList(element));
            }
        } else {
            if (index + elementLength > data.size()) {
                throw std::out_of_range("Not enough data for the element");
            }

            std::vector<uint8_t> element(data.begin() + index, data.begin() + index + elementLength);
            elements.push_back(new SMLElement(element));
            index += elementLength; // Move to the next element
        }
        // Check if we have reached the expected number of elements for the current list
        if (elements.size() >= listitems) {
            break;
        }
    }

    return elements;
}
