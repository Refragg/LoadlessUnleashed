# LoadlessUnleashed
 
A small helper program to compute and output stats based on a CSV list of loads.

This can also be used to re-encode the original video but without the loads.

Usage: 

`LoadlessUnleashed "CSV loads file path"`

Usage when LoadlessUnleashed_ENCODE_VIDEO is enabled:

`LoadlessUnleashed "CSV loads file path" "Original video file path"`

You can set the following environment variables to either 'Y' or '1' to control options related to video re-encoding:

- LoadlessUnleashed_ENCODE_VIDEO: Enable video re-encoding, this requires to pass the video file path after the CSV file path
- LoadlessUnleashed_DOUBLE_ENCODE: The default behaviour only re-encodes when splitting the video into the multiple segments, enabling this means that the video will be re-encoded when splitting the video AND while concatenating the videos together
- LoadlessUnleashed_SKIP_SPLITTING: Setting this will skip the video splitting part and go straight to the concatenation, this assumes that the video-temp folder is already filled with the segments of the video
  
This is meant to be used by the Sonic Unleashed Speedrunning community.