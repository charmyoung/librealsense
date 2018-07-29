﻿using Intel.RealSense;
using System;
using System.Collections.Generic;

public class PointCloudFilter : RealSenseProcessingBlock
{
    public Stream _textureStream = Stream.Color;

    private PointCloud _pb;
    private List<Stream> _requirments = new List<Stream>() { Stream.Depth };

    private object _lock = new object();

    private Dictionary<Stream, VideoStreamRequest> _videoStreamFilter = new Dictionary<Stream, VideoStreamRequest>();
    private Dictionary<Stream, VideoStreamRequest> _currVideoStreamFilter = new Dictionary<Stream, VideoStreamRequest>();

    public override ProcessingBlockType ProcessingType { get { return ProcessingBlockType.Multi; } }

    public void Awake()
    {
        ResetProcessingBlock();
        foreach(Stream stream in Enum.GetValues(typeof(Stream)))
        {
            _videoStreamFilter[stream] = new VideoStreamRequest();
        }
    }

    private void ResetProcessingBlock()
    {
        if (_pb != null)
            _pb.Dispose();
        _pb = new PointCloud();
    }

    public override Frame Process(Frame frame, FrameSource frameSource, FramesReleaser releaser)
    {
        lock (_lock)
        {
            using (var frameset = FrameSet.FromFrame(frame))
            {
                using (var depth = frameset.DepthFrame)
                {
                    using (var texture = frameset.FirstOrDefault<VideoFrame>(_textureStream))
                    {
                        _videoStreamFilter[depth.Profile.Stream].CopyProfile(depth);
                        _videoStreamFilter[texture.Profile.Stream].CopyProfile(texture);
                        if (_currVideoStreamFilter.Count == 0 ||
                            !_currVideoStreamFilter[depth.Profile.Stream].Equals(_videoStreamFilter[depth.Profile.Stream]) ||
                            !_currVideoStreamFilter[texture.Profile.Stream].Equals(_videoStreamFilter[texture.Profile.Stream]))
                        {
                            ResetProcessingBlock();
                            _currVideoStreamFilter[depth.Profile.Stream] = new VideoStreamRequest(depth);
                            _currVideoStreamFilter[texture.Profile.Stream] = new VideoStreamRequest(texture);
                        }
                        var points = _pb.Calculate(depth, releaser);
                        _pb.MapTexture(texture);
                        return frameSource.AllocateCompositeFrame(releaser, depth, points).AsFrame();
                    }
                }
            }
        }
    }

    public override List<Stream> Requirments()
    {
        return _requirments;
    }
}
