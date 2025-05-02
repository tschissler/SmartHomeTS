#include "SMLParser.h"
#include <memory>
#include <algorithm>

// CRC-16/IBM-SDLC table (polynomial 0x1021, reflected 0x8408)
static const uint16_t CrcTable[256] = {
    0x0000, 0x1189, 0x2312, 0x329B, 0x4624, 0x57AD, 0x6536, 0x74BF, 0x8C48,
    0x9DC1, 0xAF5A, 0xBED3, 0xCA6C, 0xDBE5, 0xE97E, 0xF8F7, 0x1081, 0x0108,
    0x3393, 0x221A, 0x56A5, 0x472C, 0x75B7, 0x643E, 0x9CC9, 0x8D40, 0xBFDB,
    0xAE52, 0xDAED, 0xCB64, 0xF9FF, 0xE876, 0x2102, 0x308B, 0x0210, 0x1399,
    0x6726, 0x76AF, 0x4434, 0x55BD, 0xAD4A, 0xBCC3, 0x8E58, 0x9FD1, 0xEB6E,
    0xFAE7, 0xC87C, 0xD9F5, 0x3183, 0x200A, 0x1291, 0x0318, 0x77A7, 0x662E,
    0x54B5, 0x453C, 0xBDCB, 0xAC42, 0x9ED9, 0x8F50, 0xFBEF, 0xEA66, 0xD8FD,
    0xC974, 0x4204, 0x538D, 0x6116, 0x709F, 0x0420, 0x15A9, 0x2732, 0x36BB,
    0xCE4C, 0xDFC5, 0xED5E, 0xFCD7, 0x8868, 0x99E1, 0xAB7A, 0xBAF3, 0x5285,
    0x430C, 0x7197, 0x601E, 0x14A1, 0x0528, 0x37B3, 0x263A, 0xDECD, 0xCF44,
    0xFDDF, 0xEC56, 0x98E9, 0x8960, 0xBBFB, 0xAA72, 0x6306, 0x728F, 0x4014,
    0x519D, 0x2522, 0x34AB, 0x0630, 0x17B9, 0xEF4E, 0xFEC7, 0xCC5C, 0xDDD5,
    0xA96A, 0xB8E3, 0x8A78, 0x9BF1, 0x7387, 0x620E, 0x5095, 0x411C, 0x35A3,
    0x242A, 0x16B1, 0x0738, 0xFFCF, 0xEE46, 0xDCDD, 0xCD54, 0xB9EB, 0xA862,
    0x9AF9, 0x8B70, 0x8408, 0x9581, 0xA71A, 0xB693, 0xC22C, 0xD3A5, 0xE13E,
    0xF0B7, 0x0840, 0x19C9, 0x2B52, 0x3ADB, 0x4E64, 0x5FED, 0x6D76, 0x7CFF,
    0x9489, 0x8500, 0xB79B, 0xA612, 0xD2AD, 0xC324, 0xF1BF, 0xE036, 0x18C1,
    0x0948, 0x3BD3, 0x2A5A, 0x5EE5, 0x4F6C, 0x7DF7, 0x6C7E, 0xA50A, 0xB483,
    0x8618, 0x9791, 0xE32E, 0xF2A7, 0xC03C, 0xD1B5, 0x2942, 0x38CB, 0x0A50,
    0x1BD9, 0x6F66, 0x7EEF, 0x4C74, 0x5DFD, 0xB58B, 0xA402, 0x9699, 0x8710,
    0xF3AF, 0xE226, 0xD0BD, 0xC134, 0x39C3, 0x284A, 0x1AD1, 0x0B58, 0x7FE7,
    0x6E6E, 0x5CF5, 0x4D7C, 0xC60C, 0xD785, 0xE51E, 0xF497, 0x8028, 0x91A1,
    0xA33A, 0xB2B3, 0x4A44, 0x5BCD, 0x6956, 0x78DF, 0x0C60, 0x1DE9, 0x2F72,
    0x3EFB, 0xD68D, 0xC704, 0xF59F, 0xE416, 0x90A9, 0x8120, 0xB3BB, 0xA232,
    0x5AC5, 0x4B4C, 0x79D7, 0x685E, 0x1CE1, 0x0D68, 0x3FF3, 0x2E7A, 0xE70E,
    0xF687, 0xC41C, 0xD595, 0xA12A, 0xB0A3, 0x8238, 0x93B1, 0x6B46, 0x7ACF,
    0x4854, 0x59DD, 0x2D62, 0x3CEB, 0x0E70, 0x1FF9, 0xF78F, 0xE606, 0xD49D,
    0xC514, 0xB1AB, 0xA022, 0x92B9, 0x8330, 0x7BC7, 0x6A4E, 0x58D5, 0x495C,
    0x3DE3, 0x2C6A, 0x1EF1, 0x0F78
};

// Compute CRC-16/IBM-SDLC over the given data
uint16_t SMLParser::ComputeCRC16(const std::vector<uint8_t>& data, size_t offset, size_t length) {
    const uint16_t Init = 0xFFFF;
    const uint16_t XorOut = 0xFFFF;
    uint16_t crc = Init;
    for (size_t i = offset; i < offset + length; ++i) {
        uint8_t b = data[i];
        crc = (crc >> 8) ^ CrcTable[(crc ^ b) & 0xFF];
    }
    return crc ^ XorOut;
}

// Verifies that the last two bytes of 'buffer' match the CRC (little-endian)
bool SMLParser::VerifyCRC16(const std::vector<uint8_t>& buffer) {
    if (buffer.size() < 2) return false;
    size_t n = buffer.size();
    uint16_t computed = ComputeCRC16(buffer, 0, n - 2);
    uint16_t received = (static_cast<uint16_t>(buffer[n - 1]) << 8) | buffer[n - 2];
    return computed == received;
}

// SMLElement Constructor
SMLElement::SMLElement(std::vector<uint8_t> d) : data(d) {}

// SMLList Constructor
SMLList::SMLList(std::vector<std::shared_ptr<ISMLNode>> e) : elements(e) {}

// SMLParser Methods
std::shared_ptr<SMLData> SMLParser::Parse(std::vector<uint8_t>& data) {
    std::vector<std::shared_ptr<ISMLNode>> smlMessages = ExtractNodes(data);
    
    if (smlMessages.empty()) {
        throw std::runtime_error("Error while parsing SML package. No SML messages could be identified");
    }
    if (smlMessages.size() < 2) {
        throw std::runtime_error("Error while parsing SML package. Expected at least 2 SML messages, but found " + std::to_string(smlMessages.size()));
    }
    if (smlMessages.at(1)->getType() != SMLNodeType::List) {
        throw std::runtime_error("Error while parsing SML package. Second element on root level is not a list");
    }

    // The whole data package delivers multiple SML messages but we are only interested in the second one
    auto dataSMLMessage = std::static_pointer_cast<SMLList>(smlMessages.at(1));
    if (dataSMLMessage->elements.size() < 4) {
        throw std::runtime_error("Error while parsing SML package. Second list does not contain enough elements");
    }
    if (!dataSMLMessage->elements.at(3)) {
        throw std::runtime_error("Error while parsing SML package. Fourth element on second SML message is null");
    }
    if (dataSMLMessage->elements.at(3)->getType() != SMLNodeType::List) {
        throw std::runtime_error("Error while parsing SML package. Fourth element on second SML message is not a list");
    }

    // That SML message should contain exactly one list element (72)
    auto dataList = FilterSMLLists(dataSMLMessage->elements);
    //std::static_pointer_cast<SMLList>(dataSMLMessage->elements.at(3));
    if (dataList.size() != 1) {
        throw std::runtime_error("Error while parsing SML package. Expected exactly one list in the SML data message but found " + dataList.size());
    }

    // In that list element we again select all sub-elements that are lists and continue by using the first list we find (77)
    auto subDataList = std::static_pointer_cast<SMLList>(FilterSMLLists(std::static_pointer_cast<SMLList>(dataList.at(0))->elements).at(0))->elements;
    if (subDataList.size() < 2) {
        throw std::runtime_error("Error while parsing SML package. Expected at least two lists in subDataLIst, but found " + subDataList.size());
    }
    

    // In that list we again search for all list elements and take the second one (77).
    // This is now the list that finally contains the data objects.
    // The data objects themselves are again list elements containing an identifier at the first element and the value in the sixth element
    auto valuesList = std::static_pointer_cast<SMLList>(FilterSMLLists(subDataList).at(1))->elements;
    if (valuesList.size() < 2) {
        throw std::runtime_error("Error while parsing SML package. Values list does not contain enough elements");
    }
        
    try {
        int tarif1 = 0;
        int tarif2 = 0;
        int Leistung = 0;

        std::vector<uint8_t> targetBytes = {0x07, 0x01, 0x00, 0x01, 0x08, 0x00, 0xFF};
        auto valueElement = SMLParser::FindElementByData(valuesList, targetBytes);
        if (valueElement) {
            tarif1 = SMLElementToInteger(std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(valueElement)->elements.at(5)));
        } 

        std::vector<uint8_t> targetBytes2 = {0x07, 0x01, 0x00, 0x02, 0x08, 0x00, 0xFF};
        auto valueElement2 = SMLParser::FindElementByData(valuesList, targetBytes2);
        if (valueElement2) {
            tarif1 = SMLElementToInteger(std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(valueElement)->elements.at(5)));
        } 

        std::vector<uint8_t> targetBytes3 = {0x07, 0x01, 0x00, 0x10, 0x07, 0x00, 0xFF};
        auto valueElement3 = SMLParser::FindElementByData(valuesList, targetBytes3);
        if (valueElement3) {
            tarif1 = SMLElementToInteger(std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(valueElement)->elements.at(5)));
        } 

        // auto tarif1Element = std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataList->elements.at(1))->elements.at(4))->elements.at(2))->elements.at(5));
        // auto tarif2Element = std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataList->elements.at(1))->elements.at(4))->elements.at(3))->elements.at(5));
        // auto LeistungsElement = std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataList->elements.at(1))->elements.at(4))->elements.at(4))->elements.at(5));

        // Print the content of std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4)) as HEX values, all HEX values should have 2 digits
        // for (size_t i = 0; i < std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4))->elements.size(); ++i) {
        //     for (size_t j = 0; j < std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4))->elements[i])->data.size(); ++j) {
        //         Serial.print(std::static_pointer_cast<SMLElement>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(std::static_pointer_cast<SMLList>(dataLevel2Element->elements.at(1))->elements.at(4))->elements.at(4))->elements[i])->data[j], HEX);
        //         Serial.print(" ");
        //     }
        // }
        // Serial.println();

        // int tarif1 = SMLElementToInteger(tarif1Element);
        // int tarif2 = SMLElementToInteger(tarif2Element);
        // int Leistung = SMLElementToInteger(LeistungsElement);

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

// Filtert alle Elemente vom Typ SMLList aus einer Liste von ISMLNode-Elementen
std::vector<std::shared_ptr<SMLList>> SMLParser::FilterSMLLists(const std::vector<std::shared_ptr<ISMLNode>>& nodes) {
    std::vector<std::shared_ptr<SMLList>> result;
    result.reserve(nodes.size()); // Reserviere Speicher für Effizienz
    
    // Für jedes Node-Element prüfen, ob es vom Typ SMLList ist
    for (const auto& node : nodes) {
        if (node && node->getType() == SMLNodeType::List) {
            // Umwandeln und zum Ergebnisvektor hinzufügen
            result.push_back(std::static_pointer_cast<SMLList>(node));
        }
    }
    
    return result;
}

std::shared_ptr<SMLList> SMLParser::FindElementByData(
    const std::vector<std::shared_ptr<ISMLNode>>& valuesList,
    const std::vector<uint8_t>& targetData) {
    
    auto it = std::find_if(valuesList.begin(), valuesList.end(),
        [&targetData](const std::shared_ptr<ISMLNode>& node) {
            if (!node || node->getType() != SMLNodeType::List) {
                return false;
            }
            
            auto list = std::static_pointer_cast<SMLList>(node);
            if (list->elements.empty() || 
                !list->elements[0] || 
                list->elements[0]->getType() != SMLNodeType::Element) {
                return false;
            }
            
            auto element = std::static_pointer_cast<SMLElement>(list->elements[0]);
            return element->data == targetData;
        });
    
    return (it != valuesList.end()) ? std::static_pointer_cast<SMLList>(*it) : nullptr;
}
