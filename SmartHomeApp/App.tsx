import * as React from 'react';
import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View, FlatList } from 'react-native';
import { Button } from 'react-native-elements'; 
import { useEffect, useState } from 'react';

interface DataItem {
  value: number;
  time: Date;
  localTime: Date;
  partitionKey: string;
}

const fetchData = async (partitionKeys: string[]) => {
  try {
    const response = await fetch('https://smarthomeweb.azurewebsites.net/api/storage/latestvaluesperkey', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(partitionKeys),
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const data = await response.json();
    console.log('Data fetched from API: ', data);
    return data;
  } catch (error) {
    console.error('Error fetching data: ', error);
    return null;
  }
};

const YourComponent = () => {
  const [data, setData] = useState<DataItem[]>([]);
  const [isLoading, setIsLoading] = useState(false); // Add a new state variable for loading status

  const partitionKeyAliases: { [key: string]: string } = {
    '1c50f3ab6224_temperature': 'Außen',
    '1420381fb608_temperature': 'Fensterbank',
  };

  const partitionKeys = ['1c50f3ab6224_temperature', '1420381fb608_temperature']; // Replace with your actual keys

  const refreshData = () => { // Define a new function for refreshing data
    setIsLoading(true);
    fetchData(partitionKeys).then((fetchedData) => {
      if (fetchedData) {
        setData(fetchedData);
      }
      setIsLoading(false);
    });
  };

  useEffect(() => {
    refreshData();
  }, []);

  return (
    <View style={styles.container}>
      <FlatList style={styles.list}
        data={data}
        keyExtractor={item => (item && item.partitionKey ? item.partitionKey.toString() : 'default')}
        renderItem={({ item }) => (
          <View style={styles.itemContainer}>
            <Text style={styles.title}>{partitionKeyAliases[item.partitionKey]}</Text>
            <View style={{ height: 1, backgroundColor: 'black' }} />
            <Text style={styles.text}>Temperatur: {item.value}°C</Text>
            <Text style={styles.text}>Zeit: {new Date(item.time).toLocaleTimeString()}</Text>
          </View>
        )}
      />
      <Button 
      title="Aktualisieren" 
      onPress={refreshData} 
      loading={isLoading} 
      disabled={isLoading} 
      buttonStyle={styles.button} 
      titleStyle={styles.buttonTitle} 
    />
    </View>
  );
};

export default YourComponent;

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#fff',
    alignItems: 'center',
    justifyContent: 'center',
  },
  list: {
    backgroundColor: '#f0f0f0',
    width: '100%',
    marginTop: 50,
  },
  text: {
    fontSize: 25,
    margin: 10,
  },
  title: {
    fontSize: 30,
    fontWeight: 'bold',
    margin: 10,
  },
  itemContainer: {
    backgroundColor: '#d0d0f0',
    width: '100%',
    margin: 10,
    padding: 10,
  },
  button: {
    backgroundColor: '#007BFF',
    padding: 10,
    borderRadius: 5,
  },
  buttonTitle: {
    color: 'white',
    fontSize: 30,
  },
});
