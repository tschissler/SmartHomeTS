#include "SMLParser.h"
#include <memory>
#include <algorithm>

// SMLElement Constructor
SMLElement::SMLElement(std::vector<uint8_t> d) : data(d) {}

// SMLList Constructor
SMLList::SMLList(std::vector<std::shared_ptr<ISMLNode>> e) : elements(e) {}

// SMLData Constructor
SMLData::SMLData(float t1, float t2, float p) : Tarif1(t1), Tarif2(t2), Power(p) {}

// SMLParser Methods
std::shared_ptr<SMLData> SMLParser::Parse(std::vector<uint8_t>& data) {
    std::vector<std::shared_ptr<ISMLNode>> elements = ExtractNodes(data);
    
    if (elements.empty()) {
        throw std::runtime_error("Error while parsing SML package. No elements could be identified");
    }

    if (elements.size() < 2) {
        throw std::runtime_error("Error while parsing SML package. Expected 2 elements on root level, but found " + std::to_string(elements.size()));
    }

    if (!elements.at(1)) {
        throw std::runtime_error("Error while parsing SML package. Second element on root level is null");
    }

    if (elements.at(1)->getType() != 2) {
        throw std::runtime_error("Error while parsing SML package. Second element on root level is not a list");
    }

    auto dataRootElement = std::static_pointer_cast<SMLList>(elements.at(1));

    if (dataRootElement->elements.size() < 4) {
        throw std::runtime_error("Error while parsing SML package. Second list does not contain enough elements");
    }

    if (!dataRootElement->elements.at(3)) {
        throw std::runtime_error("Error while parsing SML package. Fourth element on second level is null");
    }

    if (dataRootElement->elements.at(3)->getType() != 2) {
        throw std::runtime_error("Error while parsing SML package. Fourth element on second level is not a list");
    }

    auto dataLevel2Element = std::static_pointer_cast<SMLList>(dataRootElement->elements.at(3));

    if (dataLevel2Element->elements.size() < 2) {
        throw std::runtime_error("Error while parsing SML package. Third list does not contain enough elements");
    }

    if (!dataLevel2Element->elements.at(1) ||
        !std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4) ||
        !std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(2) ||
        !std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(2))->elements.at(5)) {
        throw std::runtime_error("Error while parsing SML package. Elements are null");
    }

    try {
        auto tarif1Element = std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(2))->elements.at(5));
        auto tarif2Element = std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(3))->elements.at(5));
        auto LeistungsElement = std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4))->elements.at(5));

        // Print the content of std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4)) as HEX values, all HEX values should have 2 digits
        // for (size_t i = 0; i < std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4))->elements.size(); ++i) {
        //     for (size_t j = 0; j < std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4))->elements[i])->data.size(); ++j) {
        //         Serial.print(std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4))->elements[i])->data[j], HEX);
        //         Serial.print(" ");
        //     }
        // }
        // Serial.println();

        int tarif1 = SMLElementToInteger(tarif1Element);
        int tarif2 = SMLElementToInteger(tarif2Element);
        int Leistung = SMLElementToInteger(LeistungsElement);

        return std::make_shared<SMLData>(tarif1 / 10000.0f, tarif2 / 10000.0f, Leistung);

    } catch (const std::exception& ex) {
        throw std::runtime_error("Error while parsing SML package. " + std::string(ex.what()));
    }
    return nullptr;
}

int SMLParser::SMLElementToInteger(std::shared_ptr<SMLElement> byteData) {
    if (!byteData) {
        Serial.println("Error: byteData is null");
        throw std::runtime_error("byteData is null");
    }

    // Print byteData as Hex values
    // for (size_t i = 0; i < byteData->data.size(); ++i) {
    //     Serial.print(byteData->data[i], HEX);
    //     Serial.print(" ");
    // }
    // Serial.println();

    if (byteData->data.size() < 2) {
        Serial.println("Error: byteData size is too small");
        throw std::runtime_error("byteData size is too small");
    }

    std::vector<uint8_t> tarif1Array(byteData->data.begin() + 1, byteData->data.end());
    if (isLittleEndian()) {
        std::reverse(tarif1Array.begin(), tarif1Array.end());
    }

    // Signed Integers
    if ((byteData->data.at(0) >> 4) == 5) {
        switch (byteData->data.at(0) & 0x0F)
{
        case 2:
            return *reinterpret_cast<const int8_t*>(tarif1Array.data());
        case 3:
        case 4:
            return *reinterpret_cast<const int16_t*>(tarif1Array.data());
        case 5:
        case 6:
            return *reinterpret_cast<const int32_t*>(tarif1Array.data());
        default:
            Serial.println("Error: Invalid signed integer length");
            throw std::runtime_error("Error while parsing integer, found length " + std::to_string(byteData->data.at(0) & 0x0F) + " but only 2, 3 and 5 are allowed");
        }
    }
    // Unsigned Integers
    else if ((byteData->data.at(0) >> 4) == 6) {
        switch (byteData->data.at(0) & 0x0F)
        {
        case 2:
            return *reinterpret_cast<const uint8_t*>(tarif1Array.data());
        case 3:
        case 4:
            return *reinterpret_cast<const uint16_t*>(tarif1Array.data());
        case 5:
        case 6:
            return *reinterpret_cast<const uint32_t*>(tarif1Array.data());
        default:
            Serial.println("Error: Invalid unsigned integer length");
            throw std::runtime_error("Error while parsing unsigned integer, found length " + std::to_string(byteData->data.at(0) & 0x0F) + " but only 2, 3 and 5 are allowed");
        }
    }
    Serial.println("Error: Invalid integer type");
    throw std::runtime_error("Error while parsing integer, found type " + std::to_string(byteData->data.at(0) >> 4) + " but only 5 (signed) and 6 (unsigned) are allowed");
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
            if (data.at(i + j) != startSequence.at(j)) {
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
            if (data.at(i + j) != endSequencePrefix.at(j)) {
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

std::vector<std::shared_ptr<ISMLNode>> SMLParser::ExtractNodes(std::vector<uint8_t>& data) {
    int index = 0;
    return ExtractNodes(data, index, 99);
}

std::vector<std::shared_ptr<ISMLNode>> SMLParser::ExtractNodes(std::vector<uint8_t>& data, int& index, int listitems) {
    std::vector<std::shared_ptr<ISMLNode>> elements;
    while (index < data.size()) {
        int elementType = data.at(index) >> 4; // Extract the type from the start byte
        int elementLength = data.at(index) & 0x0F; // Extract the length from the start byte
        if (elementType == 0x08) 
            elementLength = (elementLength << 4) + data.at(index + 1); 
        if (elementLength == 0)
            elementLength = 1; // If the element is 0x00, the element is 1 byte long

        if (elementType == 0x07) {
            index++;
            auto element = ExtractNodes(data, index, elementLength);
            if (element.empty() || element.size() != elementLength) {
                throw std::out_of_range("Number of elements in list does not match the specified length for the list");
            }
            else {
                elements.push_back(std::make_shared<SMLList>(element));
            }
        }
        else {
            if (index + elementLength > data.size()) {
                throw std::out_of_range("Not enough data for the element");
            }

            std::vector<uint8_t> element(data.begin() + index, data.begin() + index + elementLength);
            elements.push_back(std::make_shared<SMLElement>(element));
            index += elementLength; // Move to the next element
        }
        // Check if we have reached the expected number of elements for the current list
        if (elements.size() >= listitems) {
            break;
        }
    }

    return elements;
}
