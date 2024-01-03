namespace vcrtspd;

// Delegated functions (essentially the function prototype)
public delegate void ReceivedYUVFrameHandler(uint timestamp, int width, int height, byte[] data);
public delegate void ReceivedAudioFrameHandler(uint timestamp, short[] data);
