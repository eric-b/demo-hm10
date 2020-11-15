#include <SoftwareSerial.h>
#include <avr/wdt.h>

const uint8_t ARDUINO_RX_FROM_BLUETOOTH = 12; // PIN12 bound to TX Bluetooth module;
const uint8_t ARDUINO_TX_TO_BLUETOOTH = 13; // PIN13 bound to RX Bluetooth module;
SoftwareSerial HM18(ARDUINO_RX_FROM_BLUETOOTH, ARDUINO_TX_TO_BLUETOOTH);

const uint16_t INTERVAL_LOOP = 100;
const uint16_t TICK_STEP = 300;

const String EMPTY_STRING = "";

uint16_t _loopCount = 0;
bool _bluetoothConnected;

void setup() 
{
  Serial.begin(9600);
  setupBlueetoth();
}

void loop() 
{
    _loopCount++;
    processHM18Commands();
    delay(INTERVAL_LOOP);

    // publish some demo string periodically to bluetooth
    uint16_t x = _loopCount % TICK_STEP;
    if (x == 0)
    {
        sendHm18RawString(String(_loopCount));
    }
}

void setupBlueetoth()
{
  HM18.begin(9600);
  
  if (sendHm18AtCommand("AT").startsWith("OK")) 
  {
    setHm18("AT+NAME", "Arduino");
    // other HM-18 setup here...
  }
}

// Returns true if setting changed, false if already good or if command failed.
bool setHm18(String commandPrefix, String valueSuffix)
{
    String ack = sendHm18AtCommand(commandPrefix + '?');
    if (ack != EMPTY_STRING && ack != "OK+Get:" + valueSuffix)
    {
        return sendHm18AtCommand(commandPrefix + valueSuffix) == "OK+Set:" + valueSuffix;
    }
    return false;
}

void processHM18Commands()
{
  while (HM18.available())
  {
    String received = HM18.readString();
    Serial.println("RX BT: " + String(received.length()) + " bytes - " + received);
    if (!updateStateFromHm18Ack(received))
    {
      String response = processCommandAndGetResponse(received);
      if (response != EMPTY_STRING)
        sendHm18RawString(response);
    }
  }
}

String processCommandAndGetResponse(String command)
{
  if (command == ".state?")
  {
    // TODO: compress response to bit mask
    return "_bluetoothConnected: " + String(_bluetoothConnected) + " _loopCount: " + String(_loopCount);
  }
  else if (command == ".reboot")
  {
    reboot();
  }
  return String();
}

bool updateStateFromBluetoothAckSingle(String ack)
{
  if (ack.startsWith("OK+STARTOK+"))
  {
    // Sometimes, we get "OK+STARTOK+CONN"
    // We trim "OK+START" to correctly update meaningful state.
    ack = ack.substring(8);
  }

  if (ack == "OK+CONN")
  {
    _bluetoothConnected = true;
    return true;
  }
  else if (ack == "OK+LOST")
  {
    _bluetoothConnected = false;
    return true;
  }
  // and other state persistency if meaningful...

  return false;
}

void sendHm18RawString(String data)
{
  Serial.println("Sending to BT: " + data);
  HM18.print(data);
}

// Send command and wait for ACK.
// If multiple ack/lines received, only first one is returned.
// Internal state is updated if multiple known ACKs are parsed.
String sendHm18AtCommand(String command)
{
  uint8_t maxAttemptsCount = getMaxRetriesForHm18Command(command) + 1;
  for (uint8_t i = 0; i < maxAttemptsCount; i++)
  {
    sendHm18RawString(command);
    String ack = waitForAckFromHm18();
    if (ack != EMPTY_STRING)
    {
      // sometimes we get multiple lines: get first line only.
      ack = getFirstHm18Ack(ack);
      if (i+1 != maxAttemptsCount && command.startsWith("AT+") && ack == "OK+WAKE")
        continue; // special: sometimes we unexpectedly wake up module. Repeat command...
      if (i+1 != maxAttemptsCount && command.endsWith("?") && !ack.startsWith("OK+Get:"))
        continue; // special: retry if question not answered
      Serial.println(command + ": " + ack);
      return ack;
    }
    else
    {
      Serial.println("Timed out while waiting for ACK from BT for " + command);
    }  
  }
  return String();
}

uint8_t getMaxRetriesForHm18Command(String command)
{
  if (command == "AT" || 
      command.endsWith("?"))
  {
    return (uint8_t)3;
  }
  else
  {
    return (uint8_t)0;
  }
}

String waitForAckFromHm18()
{
  const unsigned int forCount = 300;
  const unsigned long delayInterval = 10;
  for (unsigned int i = 0; i < forCount; i++)
  {
    delay(delayInterval);
    if (HM18.available())
    {
      String ack = HM18.readString();
      updateStateFromHm18Ack(ack);
      return ack;
    }
  }

  return String();
}

String getFirstHm18Ack(String data)
{
  int index = getNextHm18AckDelimiter(data);
  if (index == -1)
    return data;
  
  return data.substring(0, index);
}

// Return -1 if no delimiter found or next index if found.
int getNextHm18AckDelimiter(String data)
{
  const String delimiter = "\r\n";
  return data.indexOf(delimiter);
}

bool updateStateFromHm18Ack(String data) 
{
  // BT module may concatenate its ack with data received from a remote device connected.
  // Data are delimited by a newline.
  // Multiple acks can be received at the same time (for eg: if we connect/disconnect quickly -> OK+CONN, OK+LOST).
  const uint8_t delimiterLength = 2;
  String remainingStr = data;
  bool foundAck = false;
  while (true)
  {
    int index = getNextHm18AckDelimiter(remainingStr);
    if (index == -1)
      return foundAck;

    String s = remainingStr.substring(0, index);
    if (s.startsWith("OK"))
    {
       if (updateStateFromBluetoothAckSingle(s))
        foundAck = true;
    }
    remainingStr = remainingStr.substring(index + delimiterLength);
  }
}

// Soft reset
void reboot() {
  wdt_disable();
  wdt_enable(WDTO_15MS);
  while (1) {}
}
