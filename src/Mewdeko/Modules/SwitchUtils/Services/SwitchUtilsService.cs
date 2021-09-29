using System.Collections.Generic;
using Mewdeko.Services;

namespace Mewdeko.Modules.SwitchUtils.Services
{
    public class SwitchUtilsService : INService
    {
        public Dictionary<string, string> switch_game_err = new()
        {
            {
                "2-AAB6A-3400",
                "Splatoon 2: A kick from online due to exefs edits."
            },
            {
                "2-ARVHA-0000",
                "Youtube: Unknown Error"
            }
        };

        public Dictionary<int, List<object>> switch_known_errcode_ranges = new()
        {
            {
                137,
                new List<object>
                {
                    new List<object>
                    {
                        8001,
                        8096,
                        "libcurl error 1-96. Some of the libcurl errors in the error-table map to the above unknown-libcurl-error however."
                    }
                }
            },
            {
                2,
                new List<object>
                {
                    new List<object>
                    {
                        2000,
                        2499,
                        "Error: Failed to access SD card."
                    },
                    new List<object>
                    {
                        2500,
                        2999,
                        "Error: Failed to access game card. "
                    },
                    new List<object>
                    {
                        3500,
                        3999,
                        "Error: Failed to access MMC. "
                    },
                    new List<object>
                    {
                        4001,
                        4299,
                        "Error: ROM is corrupted. "
                    },
                    new List<object>
                    {
                        4301,
                        4499,
                        "Error: Save data is corrupted."
                    },
                    new List<object>
                    {
                        4501,
                        4599,
                        "Error: NCA is corrupted."
                    },
                    new List<object>
                    {
                        4601,
                        4639,
                        "Error: Integrity verification failed."
                    },
                    new List<object>
                    {
                        4641,
                        4659,
                        "Error: Partition FS is corrupted."
                    },
                    new List<object>
                    {
                        4661,
                        4679,
                        "Error: Built-in-storage is corrupted."
                    },
                    new List<object>
                    {
                        4681,
                        4699,
                        "Error: FAT FS is corrupted."
                    },
                    new List<object>
                    {
                        4701,
                        4719,
                        "Error: HOST FS is corrupted."
                    },
                    new List<object>
                    {
                        5000,
                        5999,
                        "Error: Unexpected failure occurred."
                    },
                    new List<object>
                    {
                        6002,
                        6029,
                        "Error: Invalid path was specified."
                    },
                    new List<object>
                    {
                        6001,
                        6199,
                        "Error: Invalid argument was specified."
                    },
                    new List<object>
                    {
                        6202,
                        6299,
                        "Error: Invalid operation for the open mode."
                    },
                    new List<object>
                    {
                        6300,
                        6399,
                        "Error: Unsupported operation."
                    },
                    new List<object>
                    {
                        6400,
                        6499,
                        "Error: Permission denied."
                    }
                }
            },
            {
                110,
                new List<object>
                {
                    new List<object>
                    {
                        2900,
                        2999,
                        "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22277/p/897"
                    },
                    new List<object>
                    {
                        2000,
                        2899,
                        "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22263/p/897"
                    }
                }
            }
        };

        public Dictionary<int, string> switch_known_errcodes = new()
        {
            {
                0x0E01,
                "Session count exceeded "
            },
            {
                0x1C01,
                "Invalid kernel capability descriptor "
            },
            {
                0x4201,
                "Not Implemented "
            },
            {
                0x7601,
                "Thread terminated/termination requested "
            },
            {
                0x8C01,
                "No more debug events "
            },
            {
                0xCA01,
                "Invalid size "
            },
            {
                0xCC01,
                "Invalid address "
            },
            {
                0xCE01,
                "Resource exhaustion "
            },
            {
                0xD001,
                "Memory exhaustion "
            },
            {
                0xD201,
                "Handle-table exhaustion "
            },
            {
                0xD801,
                "Invalid memory permissions. "
            },
            {
                0xDC01,
                "Invalid memory range "
            },
            {
                0xE001,
                "Invalid thread priority. "
            },
            {
                0xE201,
                "Invalid processor id. "
            },
            {
                0xE401,
                "Invalid handle. "
            },
            {
                0xE601,
                "Invalid pointer/Syscall copy from user failed. "
            },
            {
                0xE801,
                "Invalid combination "
            },
            {
                0xEA01,
                "Time out. Also when you give 0 handles to svcWaitSynchronizationN. "
            },
            {
                0xEC01,
                "Operation canceled"
            },
            {
                0xEE01,
                "Out of range "
            },
            {
                0xF001,
                "Invalid enum "
            },
            {
                0xF201,
                "No such entry "
            },
            {
                0xF401,
                "Irq/DeviceAddressSpace/{...} already registered "
            },
            {
                0xF601,
                "Port remote dead "
            },
            {
                0xF801,
                "[Usermode] Unhandled interrupt/exception "
            },
            {
                0xFA01,
                "Process already started/Wrong memory permission? "
            },
            {
                0xFC01,
                "Reserved value "
            },
            {
                0xFE01,
                "Invalid hardware breakpoint "
            },
            {
                0x10001,
                "[Usermode] Fatal exception "
            },
            {
                0x10201,
                "Last thread didn't belong to your process "
            },
            {
                0x10601,
                "Port closed "
            },
            {
                0x10801,
                "Resource limit exceeded "
            },
            {
                0x20801,
                "Command buffer too small "
            },
            {
                0x40A01,
                "Invalid process ID."
            },
            {
                0x40C01,
                "Invalid thread ID."
            },
            {
                0x40E01,
                "Invalid thread ID (used in svcGetDebugThreadParam)."
            },
            {
                0x6402,
                "NCA is older than version 3, or NCA SDK version is older than 0.11.0.0"
            },
            {
                0x1408,
                "Program location for debug entry not found"
            },
            {
                0x40080,
                "Unbalanced fatal section leave (returned when executing LeaveFatalSection without a preceding EnterFatalSection call)"
            },
            {
                0x41001,
                "Process not being debugged "
            },
            {
                0x202,
                "Path does not exist. "
            },
            {
                0x402,
                "Path already exists. "
            },
            {
                0x4602,
                "Not enough free space for BIS Calibration partition. "
            },
            {
                0x4802,
                "Not enough free space for BIS Safe partition. "
            },
            {
                0x4A02,
                "Not enough free space for BIS User partition. "
            },
            {
                0x4C02,
                "Not enough free space for BIS System partition. "
            },
            {
                0x4E02,
                "Not enough free space on SD card. "
            },
            {
                0x7802,
                "Specified mount name already exists."
            },
            {
                0x7D202,
                "Process does not have RomFs "
            },
            {
                0x7D402,
                "Title-id not found / savedata not found. "
            },
            {
                0xFA202,
                "SD card not inserted "
            },
            {
                0x13B002,
                "Gamecard not inserted "
            },
            {
                0x171402,
                "Invalid gamecard handle. "
            },
            {
                0x177202,
                "Unimplemented behavior "
            },
            {
                0x177602,
                "File/Directory already exists. "
            },
            {
                0x190202,
                "Memory allocation failure related to FAT filesystem code "
            },
            {
                0x190602,
                "Memory allocation failure related to FAT filesystem code "
            },
            {
                0x190802,
                "Memory allocation failure related to FAT filesystem code "
            },
            {
                0x190A02,
                "Memory allocation failure related to FAT filesystem code "
            },
            {
                0x190C02,
                "Memory allocation failure related to FAT filesystem code "
            },
            {
                0x191002,
                "Memory allocation failure related to FAT filesystem code "
            },
            {
                0x195802,
                "Allocation failure related to SD cards "
            },
            {
                0x196002,
                "Out of memory "
            },
            {
                0x196202,
                "Out of memory "
            },
            {
                0x1A3E02,
                "Out of memory "
            },
            {
                0x1A4002,
                "Out of memory "
            },
            {
                0x1A4A02,
                "Out of memory "
            },
            {
                0x21BC02,
                "Invalid save data filesystem magic (valid magic is SAVE in ASCII) "
            },
            {
                0x234202,
                "Error reading ACID section in NPDM "
            },
            {
                0x234402,
                "Invalid NPDM ACID section size "
            },
            {
                0x234602,
                "Last byte of the ACID modulus is zero "
            },
            {
                0x234802,
                "Invalid ACID fixed key signature "
            },
            {
                0x234A02,
                "Invalid NCA magic "
            },
            {
                0x234C02,
                "Invalid NCA header fixed key signature "
            },
            {
                0x234E02,
                "Invalid NCA header ACID signature "
            },
            {
                0x235002,
                "Invalid NCA header section hash "
            },
            {
                0x235202,
                "Invalid NCA Key index "
            },
            {
                0x235602,
                "Invalid encryption type "
            },
            {
                0x235802,
                "Redirection BKTR table size is negative "
            },
            {
                0x235A02,
                "Encryption BKTR table size is negative "
            },
            {
                0x235C02,
                "Redirection BKTR table end offset is past the Encryption BKTR table start offset "
            },
            {
                0x235E02,
                "NCA-path used with the wrong titleID. "
            },
            {
                0x236002,
                "NCA header value is out of range "
            },
            {
                0x236202,
                "NCA FS header value is out of range "
            },
            {
                0x236802,
                "PartitionFS hash block size is not a power of 2 "
            },
            {
                0x236A02,
                "PartitionFS hash always_2 field is not 2 "
            },
            {
                0x236C02,
                "PartitionFS hash table is too small for main data "
            },
            {
                0x236E02,
                "Invalid PartitionFS block hash "
            },
            {
                0x249802,
                "Invalid FAT file number. "
            },
            {
                0x249C02,
                "Invalid FAT format for BIS User partition. "
            },
            {
                0x249E02,
                "Invalid FAT format for BIS System partition. "
            },
            {
                0x24A002,
                "Invalid FAT format for BIS Safe partition. "
            },
            {
                0x24A202,
                "Invalid FAT format for BIS Calibration partition. "
            },
            {
                0x250E02,
                "Corrupted NAX0 header. "
            },
            {
                0x251002,
                "Invalid NAX0 magicnum. "
            },
            {
                0x280202,
                "Invalid FAT size "
            },
            {
                0x280402,
                "Invalid FAT BPB (BIOS Parameter Block) "
            },
            {
                0x280602,
                "Invalid FAT parameter "
            },
            {
                0x280802,
                "Invalid FAT sector "
            },
            {
                0x280A02,
                "Invalid FAT sector "
            },
            {
                0x280C02,
                "Invalid FAT sector "
            },
            {
                0x280E02,
                "Invalid FAT sector "
            },
            {
                0x296A02,
                "Mountpoint not found "
            },
            {
                0x2EE202,
                "Invalid input "
            },
            {
                0x2EE602,
                "Path too long "
            },
            {
                0x2EE802,
                "Invalid character. "
            },
            {
                0x2EEA02,
                "Invalid directory path. "
            },
            {
                0x2EEC02,
                "Unable to retrieve directory from path "
            },
            {
                0x2F5A02,
                "Offset outside storage "
            },
            {
                0x313802,
                "Operation not supported "
            },
            {
                0x320002,
                "Permission denied "
            },
            {
                0x326602,
                "Missing titlekey(?) required to mount content "
            },
            {
                0x326E02,
                "File not closed "
            },
            {
                0x327002,
                "Directory not closed "
            },
            {
                0x327402,
                "FS allocators already registered "
            },
            {
                0x327602,
                "FS allocators already used "
            },
            {
                0x339402,
                "File not found. "
            },
            {
                0x339602,
                "Directory not found. "
            },
            {
                0x803,
                "OS busy "
            },
            {
                0xE03,
                "Invalid parameter "
            },
            {
                0x1003,
                "Out of memory "
            },
            {
                0x1203,
                "Out of resources "
            },
            {
                0x3EA03,
                "Invalid handle "
            },
            {
                0x3EE03,
                "Invalid memory mirror "
            },
            {
                0x7FE03,
                "TLS slot is not allocated "
            },
            {
                0xA05,
                "NcaID not found. Returned when attempting to mount titles which exist that aren't *8XX titles, the same way *8XX titles are mounted. "
            },
            {
                0xE05,
                "TitleId not found "
            },
            {
                0x1805,
                "Invalid StorageId "
            },
            {
                0xDC05,
                "Gamecard not inserted "
            },
            {
                0x17C05,
                "Gamecard not initialized "
            },
            {
                0x1F405,
                "Sdcard not inserted "
            },
            {
                0x20805,
                "Storage not mounted "
            },
            {
                0x806,
                "Converted from error 0xD401 "
            },
            {
                0x1006,
                "Converted from error 0xE401 "
            },
            {
                0x408,
                "Program location entry not found "
            },
            {
                0x608,
                "Invalid context for control location "
            },
            {
                0x808,
                "Storage not found "
            },
            {
                0xA08,
                "Access denied "
            },
            {
                0xC08,
                "Offline manual HTML location entry not found "
            },
            {
                0xE08,
                "Title is not registered "
            },
            {
                0x1008,
                "Control location entry for host not found "
            },
            {
                0x1208,
                "Legal info HTML location entry not found "
            },
            {
                0x209,
                "Args too long. "
            },
            {
                0x409,
                "Maximum processes loaded. "
            },
            {
                0x609,
                "NPDM too big. "
            },
            {
                0x19009,
                "Invalid access control sizes in NPDM. "
            },
            {
                0x809,
                "Invalid NPDM. "
            },
            {
                0xA09,
                "Invalid files. "
            },
            {
                0xE09,
                "Already registered. "
            },
            {
                0x1009,
                "Title not found. "
            },
            {
                0x1209,
                "Title-id in ACI0 doesn't match range in ACID. "
            },
            {
                0x6609,
                "Invalid memory state/permission "
            },
            {
                0x6A09,
                "Invalid NRR "
            },
            {
                0xA209,
                "Unaligned NRR address "
            },
            {
                0xA409,
                "Bad NRR size "
            },
            {
                0xAA09,
                "Bad NRR address "
            },
            {
                0xAE09,
                "Bad initialization "
            },
            {
                0xC809,
                "Unknown ACI0 descriptor "
            },
            {
                0xCE09,
                "ACID/ACI0 don't match for descriptor KernelFlags "
            },
            {
                0xD009,
                "ACID/ACI0 don't match for descriptor SyscallMask "
            },
            {
                0xD409,
                "ACID/ACI0 don't match for descriptor MapIoOrNormalRange "
            },
            {
                0xD609,
                "ACID/ACI0 don't match for descriptor MapNormalPage "
            },
            {
                0xDE09,
                "ACID/ACI0 don't match for descriptor InterruptPair "
            },
            {
                0xE209,
                "ACID/ACI0 don't match for descriptor ApplicationType "
            },
            {
                0xE409,
                "ACID/ACI0 don't match for descriptor KernelReleaseVersion "
            },
            {
                0xE609,
                "ACID/ACI0 don't match for descriptor HandleTableSize "
            },
            {
                0xE809,
                "ACID/ACI0 don't match for descriptor DebugFlags "
            },
            {
                0x1940A,
                "Invalid CMIF header size. "
            },
            {
                0x1A60A,
                "Invalid CMIF input header. "
            },
            {
                0x1A80A,
                "Invalid CMIF output header. "
            },
            {
                0x1BA0A,
                "Invalid method dispatch ID. "
            },
            {
                0x1D60A,
                "Invalid in object count. "
            },
            {
                0x1D80A,
                "Invalid out object count. "
            },
            {
                0x25A0A,
                "Out of domain entries. "
            },
            {
                0x20B,
                "Unsupported operation "
            },
            {
                0xCC0B,
                "Out of server session memory "
            },
            {
                0x11A0B,
                "Went past maximum during marshalling. "
            },
            {
                0x1900B,
                "Session doesn't support domains. "
            },
            {
                0x25A0B,
                "Remote process is dead. "
            },
            {
                0x3260B,
                "Unknown request type "
            },
            {
                0x3D60B,
                "IPC Query 1 failed. "
            },
            {
                0x20F,
                "Pid not found "
            },
            {
                0x60F,
                "Process has no pending events "
            },
            {
                0xA0F,
                "Application already running "
            },
            {
                0x410,
                "Title-id not found "
            },
            {
                0xF010,
                "Gamecard sysupdate not required "
            },
            {
                0x1F610,
                "Unexpected StorageId "
            },
            {
                0x215,
                "Out of processes "
            },
            {
                0x415,
                "Not initialized. "
            },
            {
                0x615,
                "Max sessions "
            },
            {
                0x815,
                "Service already registered "
            },
            {
                0xA15,
                "Out of services "
            },
            {
                0xC15,
                "Invalid name (all zeroes) "
            },
            {
                0xE15,
                "Service not registered "
            },
            {
                0x1015,
                "Permission denied "
            },
            {
                0x1215,
                "Service Access Control too big. "
            },
            {
                0x416,
                "Address space is full "
            },
            {
                0x616,
                "NRO already loaded "
            },
            {
                0x816,
                "Invalid NRO header values "
            },
            {
                0xC16,
                "Bad NRR magic "
            },
            {
                0x1016,
                "Reached max NRR count "
            },
            {
                0x1216,
                "Unable to verify NRO hash or NRR signature "
            },
            {
                0x80216,
                "Address not page-aligned "
            },
            {
                0x80416,
                "Incorrect NRO size "
            },
            {
                0x80816,
                "NRO not loaded "
            },
            {
                0x80A16,
                "NRR not loaded "
            },
            {
                0x80C16,
                "Already initialized "
            },
            {
                0x80E16,
                "Not initialized "
            },
            {
                0x41A,
                "SMC argument is invalid"
            },
            {
                0xC81A,
                "Invalid (buffer) size"
            },
            {
                0xCA1A,
                "Decryption failure"
            },
            {
                0xD01A,
                "No AES keyslots available"
            },
            {
                0xD21A,
                "Invalid AES keyslot"
            },
            {
                0x19669,
                "Null settings value"
            },
            {
                0x1A069,
                "Null setting value size buffer "
            },
            {
                0x1A269,
                "Null debug mode flag buffer "
            },
            {
                0x1BA69,
                "Setting group name has zero length "
            },
            {
                0x1BC69,
                "Empty settings item key "
            },
            {
                0x1E269,
                "Setting group name is too long (64 character limit?) "
            },
            {
                0x1E469,
                "Setting name is too long (64 character limit?) "
            },
            {
                0x20A69,
                "Setting group name ends with '.' or contains invalid characters (allowed: [a-z0-9_\\-.]) "
            },
            {
                0x20C69,
                "Setting name ends with '.' or contains invalid characters (allowed: [a-z0-9_\\-.]) "
            },
            {
                0x4DA69,
                "Null language code buffer "
            },
            {
                0x4EE69,
                "Null network settings buffer "
            },
            {
                0x4F069,
                "Null network settings output count buffer "
            },
            {
                0x50269,
                "Null backlight settings buffer "
            },
            {
                0x51669,
                "Null Bluetooth device setting buffer "
            },
            {
                0x51869,
                "Null Bluetooth device setting output count buffer "
            },
            {
                0x51A69,
                "Null Bluetooth enable flag buffer "
            },
            {
                0x51C69,
                "Null Bluetooth AFH enable flag buffer "
            },
            {
                0x51E69,
                "Null Bluetooth boost enable flag buffer "
            },
            {
                0x52069,
                "Null BLE pairing settings buffer "
            },
            {
                0x52269,
                "Null BLE pairing settings entry count buffer "
            },
            {
                0x52A69,
                "Null external steady clock source ID buffer "
            },
            {
                0x52C69,
                "Null user system clock context buffer "
            },
            {
                0x52E69,
                "Null network system clock context buffer "
            },
            {
                0x53069,
                "Null user system clock automatic correction enabled flag buffer "
            },
            {
                0x53269,
                "Null shutdown RTC value buffer "
            },
            {
                0x53469,
                "Null external steady clock internal offset buffer "
            },
            {
                0x53E69,
                "Null account settings buffer "
            },
            {
                0x55269,
                "Null audio volume buffer "
            },
            {
                0x55669,
                "Null ForceMuteOnHeadphoneRemoved buffer "
            },
            {
                0x55869,
                "Null headphone volume warning count buffer "
            },
            {
                0x55E69,
                "Invalid audio output mode "
            },
            {
                0x56069,
                "Null headphone volume update flag buffer "
            },
            {
                0x56669,
                "Null console information upload flag buffer "
            },
            {
                0x57A69,
                "Null automatic application download flag buffer "
            },
            {
                0x57C69,
                "Null notification settings buffer "
            },
            {
                0x57E69,
                "Null account notification settings entry count buffer "
            },
            {
                0x58069,
                "Null account notification settings buffer "
            },
            {
                0x58E69,
                "Null vibration master volume buffer "
            },
            {
                0x59069,
                "Null NX controller settings buffer "
            },
            {
                0x59269,
                "Null NX controller settings entry count buffer "
            },
            {
                0x59469,
                "Null USB full key enable flag buffer "
            },
            {
                0x5A269,
                "Null TV settings buffer "
            },
            {
                0x5A469,
                "Null EDID buffer "
            },
            {
                0x5B669,
                "Null data deletion settings buffer "
            },
            {
                0x5CA69,
                "Null initial system applet program ID buffer "
            },
            {
                0x5CC69,
                "Null overlay disp program ID buffer "
            },
            {
                0x5CE69,
                "Null IsInRepairProcess buffer "
            },
            {
                0x5D069,
                "Null RequiresRunRepairTimeReviser buffer "
            },
            {
                0x5DE69,
                "Null device timezone location name buffer "
            },
            {
                0x5F269,
                "Null primary album storage buffer "
            },
            {
                0x60669,
                "Null USB 3.0 enable flag buffer "
            },
            {
                0x60869,
                "Null USB Type-C power source circuit version buffer "
            },
            {
                0x61A69,
                "Null battery lot buffer "
            },
            {
                0x62E69,
                "Null serial number buffer "
            },
            {
                0x64269,
                "Null lock screen flag buffer "
            },
            {
                0x64669,
                "Null color set ID buffer "
            },
            {
                0x64869,
                "Null quest flag buffer "
            },
            {
                0x64A69,
                "Null wireless certification file size buffer "
            },
            {
                0x64C69,
                "Null wireless certification file buffer "
            },
            {
                0x64E69,
                "Null initial launch settings buffer "
            },
            {
                0x65069,
                "Null device nickname buffer "
            },
            {
                0x65269,
                "Null battery percentage flag buffer "
            },
            {
                0x65469,
                "Null applet launch flags buffer "
            },
            {
                0x7E869,
                "Null wireless LAN enable flag buffer "
            },
            {
                0x7FA69,
                "Null product model buffer "
            },
            {
                0x80E69,
                "Null NFC enable flag buffer "
            },
            {
                0x82269,
                "Null ECI device certificate buffer "
            },
            {
                0x82469,
                "Null E-Ticket device certificate buffer "
            },
            {
                0x83669,
                "Null sleep settings buffer "
            },
            {
                0x84A69,
                "Null EULA version buffer "
            },
            {
                0x84C69,
                "Null EULA version entry count buffer "
            },
            {
                0x85E69,
                "Null LDN channel buffer "
            },
            {
                0x87269,
                "Null SSL key buffer "
            },
            {
                0x87469,
                "Null SSL certificate buffer "
            },
            {
                0x88669,
                "Null telemetry flags buffer "
            },
            {
                0x89A69,
                "Null Gamecard key buffer "
            },
            {
                0x89C69,
                "Null Gamecard certificate buffer "
            },
            {
                0x8AE69,
                "Null PTM battery lot buffer "
            },
            {
                0x8B069,
                "Null PTM fuel gauge parameter buffer "
            },
            {
                0x8C269,
                "Null ECI device key buffer "
            },
            {
                0x8C469,
                "Null E-Ticket device key buffer "
            },
            {
                0x8D669,
                "Null speaker parameter buffer "
            },
            {
                0x8EA69,
                "Null firmware version buffer "
            },
            {
                0x8EC69,
                "Null firmware version digest buffer "
            },
            {
                0x8EE69,
                "Null rebootless system update version buffer "
            },
            {
                0x8FE69,
                "Null Mii author ID buffer "
            },
            {
                0x91269,
                "Null fatal flags buffer "
            },
            {
                0x92669,
                "Null auto update enable flag buffer "
            },
            {
                0x93A69,
                "Null external RTC reset flag buffer "
            },
            {
                0x94E69,
                "Null push notification activity mode buffer "
            },
            {
                0x96269,
                "Null service discovery control setting buffer "
            },
            {
                0x97669,
                "Null error report share permission buffer "
            },
            {
                0x98A69,
                "Null LCD vendor ID buffer "
            },
            {
                0x99E69,
                "Null console SixAxis sensor acceleration bias buffer "
            },
            {
                0x9A069,
                "Null console SixAxis sensor angular velocity bias buffer "
            },
            {
                0x9A269,
                "Null console SixAxis sensor acceleration gain buffer "
            },
            {
                0x9A469,
                "Null console SixAxis sensor angular velocity gain buffer "
            },
            {
                0x9A669,
                "Null console SixAxis sensor angular velocity time bias buffer "
            },
            {
                0x9A869,
                "Null console SixAxis sensor angular acceleration buffer "
            },
            {
                0x9B269,
                "Null keyboard layout buffer "
            },
            {
                0x9BA69,
                "Invalid keyboard layout "
            },
            {
                0x9C669,
                "Null web inspector flag buffer "
            },
            {
                0x9C869,
                "Null allowed SSL hosts buffer "
            },
            {
                0x9CA69,
                "Null allowed SSL hosts entry count buffer "
            },
            {
                0x9CC69,
                "Null host FS mount point buffer "
            },
            {
                0x9EE69,
                "Null Amiibo key buffer "
            },
            {
                0x9F069,
                "Null Amiibo ECQV certificate buffer "
            },
            {
                0x9F269,
                "Null Amiibo ECDSA certificate buffer "
            },
            {
                0x9F469,
                "Null Amiibo ECQV BLS key buffer "
            },
            {
                0x9F669,
                "Null Amiibo ECQV BLS certificate buffer "
            },
            {
                0x9F869,
                "Null Amiibo ECQV BLS root certificate buffer "
            },
            {
                0x272,
                "Generic error "
            },
            {
                0xCC74,
                "Time not set "
            },
            {
                0x287C,
                "Argument is NULL "
            },
            {
                0x2C7C,
                "Argument is invalid "
            },
            {
                0x3C7C,
                "Bad input buffer size "
            },
            {
                0x407C,
                "Invalid input buffer "
            },
            {
                0x4680,
                "Error while launching applet/application. "
            },
            {
                0x4A80,
                "Title-ID not found. Caused by code 0x410 when applet launch fails "
            },
            {
                0x3E880,
                "Invalid IStorage size (negative?) "
            },
            {
                0x3EC80,
                "IStorage has already been opened by another accessor "
            },
            {
                0x3EE80,
                "IStorage Read/Write out-of-bounds "
            },
            {
                0x3FE80,
                "IStorage opened as wrong type (data opened as transfermem, transfermem opened as data) "
            },
            {
                0x4B080,
                "Failed to allocate memory for IStorage "
            },
            {
                0x59080,
                "Thread stack pool exhausted (out of memory) "
            },
            {
                0x7A880,
                "am.debug!dev_function setting needs to be set "
            },
            {
                0xA83,
                "Unrecognized applet ID "
            },
            {
                0x3CF089,
                "Unknown/invalid libcurl error. "
            },
            {
                0x68A,
                "Not initialized. "
            },
            {
                0x668C,
                "USB data-transfer in progress "
            },
            {
                0xD48C,
                "Invalid descriptor "
            },
            {
                0x1928C,
                "USB device not bound / interface already enabled "
            },
            {
                0x299,
                "Invalid audio device "
            },
            {
                0x499,
                "Operation couldn't complete successfully "
            },
            {
                0x699,
                "Invalid sample rate "
            },
            {
                0x899,
                "Buffer size too small "
            },
            {
                0x1099,
                "Too many buffers are still unreleased "
            },
            {
                0x1499,
                "Invalid channel count "
            },
            {
                0x40299,
                "Invalid/Unsupported operation "
            },
            {
                0xC0099,
                "Invalid handle "
            },
            {
                0xC0899,
                "Audio output was already started "
            },
            {
                0x3C9D,
                "Address is NULL "
            },
            {
                0x3E9D,
                "PID is NULL "
            },
            {
                0x549D,
                "Already bound "
            },
            {
                0xCC9D,
                "Invalid PID "
            },
            {
                0xAA3,
                "System is booting up repair process without VOL+ held down. "
            },
            {
                0xCA3,
                "System is booting up repair process that requires RepairTimeReviser but does not have special cartridge inserted. "
            },
            {
                0xF0CD,
                "IR image data not available/ready. "
            },
            {
                0x35B,
                "Failed to init SM. "
            },
            {
                0x55B,
                "Failed to init FS. "
            },
            {
                0x75B,
                "Failed to to open NRO file. May also happen when SD card isn't inserted / SD mounting failed earlier. "
            },
            {
                0x95B,
                "Failed to read NRO header. "
            },
            {
                0xB5B,
                "Invalid NRO magic. "
            },
            {
                0xD5B,
                "Invalid NRO segments. "
            },
            {
                0xF5B,
                "Failed to read NRO. "
            },
            {
                0x135B,
                "Failed to allocate heap. "
            },
            {
                0x255B,
                "Failed to map code-binary memory. "
            },
            {
                0x275B,
                "Failed to map code memory (.text). "
            },
            {
                0x295B,
                "Failed to map code memory (.rodata). "
            },
            {
                0x2B5B,
                "Failed to map code memory (.data+.bss). "
            },
            {
                0x315B,
                "Failed to unmap code memory (.text). "
            },
            {
                0x335B,
                "Failed to unmap code memory (.rodata). "
            },
            {
                0x355B,
                "Failed to unmap code memory (.data+.bss). "
            },
            {
                0x190B2,
                "File storage failure"
            },
            {
                0x12CB2,
                "Service unavailable"
            },
            {
                0xCAB2,
                "User does not exist"
            },
            {
                0xC8B2,
                "Invalid user ID"
            },
            {
                0x885,
                "Invalid parameter"
            },
            {
                0x485,
                "Invalid DVFS table ID"
            },
            {
                0x480,
                "Storage not available."
            },
            {
                0x1987E,
                "Development/debug-only behavior"
            },
            {
                0xD27E,
                "Invalid database entry count"
            },
            {
                0xCE7E,
                "Invalid database signature value (should be \"NFDB\")"
            },
            {
                0x87E,
                "Entry not found"
            },
            {
                0x27E,
                "Invalid argument"
            },
            {
                0x7BC74,
                "Unimplemented functionality"
            },
            {
                0x7BA74,
                "TimeZone location name not found"
            },
            {
                0x70C74,
                "Value out of valid range"
            },
            {
                0x70A74,
                "Null pointer"
            },
            {
                0x64274,
                "Memory allocation failure"
            },
            {
                0x19274,
                "Signed overflow/underflow"
            },
            {
                0x19074,
                "Invalid time comparison"
            },
            {
                0xC72,
                "Unsupported operation"
            },
            {
                0x74,
                "Time service not initialized"
            },
            {
                0x19A69,
                "Null settings value buffer"
            },
            {
                0x19469,
                "Null settings key"
            },
            {
                0x19269,
                "Null settings name"
            },
            {
                0x3DD402,
                "RomFs table: Path is file, not directory"
            },
            {
                0x3DCC02,
                "RomFs directory has no more child directories/files when iterating"
            },
            {
                0x3DCA02,
                "RomFs table: Unable to add new file"
            },
            {
                0x3DC802,
                "RomFs table: Unable to add new directory"
            },
            {
                0x3DC602,
                "RomFs table: Insufficient space to add new entry"
            },
            {
                0x3DC402,
                "RomFs table: Entry already exists when trying to add a new entry"
            },
            {
                0x3DC002,
                "RomFs table: Directory not found"
            },
            {
                0x3DBE02,
                "RomFs table: File not found"
            },
            {
                0x3DBC02,
                "RomFs table: Entry not found in dictionary"
            },
            {
                0x327A02,
                "FS allocator alignment violation"
            },
            {
                0x244E02,
                "Calculated Partition FS header size changed between 2 consecutive reads"
            },
            {
                0x244C02,
                "Invalid name offset for Partition FS file entry"
            },
            {
                0x244A02,
                "Invalid HFS0 magic"
            },
            {
                0x244802,
                "Invalid PFS0 magic"
            },
            {
                0x244602,
                "Invalid Partition FS hash"
            },
            {
                0x244402,
                "Invalid Partition file hashed region bounds"
            },
            {
                0xE02,
                "Resource already in use (file already opened, savedata filesystem already mounted)."
            },
            {
                0x31AE02,
                "Operation not supported in nn::fssystem::ConcatenationFileSystem"
            },
            {
                0x31B002,
                "Operation not supported in nn::fssystem::ConcatenationFile"
            },
            {
                0x327202,
                "Writable file not closed when committing"
            },
            {
                0x35F202,
                "Mount name not found in table."
            },
            {
                0x21A,
                "SMC is not implemented"
            },
            {
                0x61A,
                "SMC is currently in progress/secmon is busy"
            },
            {
                0x81A,
                "Secmon not currently performing async operation"
            },
            {
                0xA1A,
                "Invalid SMC async callback key"
            },
            {
                0xC1A,
                "SMC is blacklisted during current boot"
            },
            {
                0xD41A,
                "BootReason already set"
            },
            {
                0xD61A,
                "BootReason not set"
            },
            {
                0xD81A,
                "Invalid argument"
            },
            {
                0x70E74,
                "TimeZoneRule conversion failed"
            },
            {
                0x27A,
                "Object not found"
            },
            {
                0x67A,
                "Object locked/in used"
            },
            {
                0x87A,
                "Target already mounted"
            },
            {
                0xA7A,
                "Target not mounted"
            },
            {
                0xC7A,
                "Object already opened"
            },
            {
                0xE7A,
                "Object not opened"
            },
            {
                0xA07A,
                "Passphrase not found"
            },
            {
                0xA27A,
                "Data verification failed"
            },
            {
                0xB47A,
                "Invalid API call"
            },
            {
                0xC47A,
                "Invalid operation"
            },
            {
                0x290,
                "Exited Abnormally ([[Applet_Manager_services#LibraryAppletExitReason|ExitReason]] == Abormal)"
            },
            {
                0x690,
                "Canceled ([[Applet_Manager_services#LibraryAppletExitReason|ExitReason]] == Canceled)"
            },
            {
                0x890,
                "Rejected"
            },
            {
                0xA90,
                "Exited Unexpectedly ([[Applet_Manager_services#LibraryAppletExitReason|ExitReason]] == Unexpected)"
            },
            {
                0x58ACA,
                "Npad ID is out of range."
            },
            {
                0x1A8CD,
                "IR camera handle pointer is null."
            },
            {
                0x198CD,
                "IR camera invalid handle value."
            },
            {
                0xD401,
                "Error: Passed buffer is not usable for fs library. "
            },
            {
                0x177A02,
                "Error: Specified value is out of range. "
            },
            {
                0x2F5C02,
                "Error: Invalid size was specified."
            },
            {
                0x2F5E02,
                "Error: Null pointer argument was specified. "
            },
            {
                0x2EE002,
                "Error: Precondition violation. "
            },
            {
                0x307202,
                "Error: OpenMode_AllowAppend is required for implicit extension of file size by WriteFile(). "
            },
            {
                0x346402,
                "Error: Enough journal space is not left. "
            },
            {
                0x346A02,
                "Error: The open count of files and directories reached the limitation. "
            },
            {
                0x4A2,
                "Can be triggered by running svcBreak. The svcBreak params have no affect on the value of the thrown error-code."
            },
            {
                0xA8,
                "Userland ARM undefined instruction exception"
            },
            {
                0x2A8,
                "Userland ARM prefetch-abort due to PC set to non-executable region"
            },
            {
                0x4A8,
                "Userland ARM data abort. Also caused by abnormal process termination via svcExitProcess. Note: directly jumping to nnMain()-retaddr from non-main-thread has the same result."
            },
            {
                0x6A8,
                "Userland PC address not aligned to 4 bytes "
            },
            {
                0x10A8,
                "Can occur when attempting to call an svc outside the whitelist "
            },
            {
                0x359,
                "LibnxError_BadReloc"
            },
            {
                0x559,
                "LibnxError_OutOfMemory"
            },
            {
                0x759,
                "LibnxError_AlreadyMapped"
            },
            {
                0x959,
                "LibnxError_BadGetInfo_Stack"
            },
            {
                0xB59,
                "LibnxError_BadGetInfo_Heap"
            },
            {
                0xD59,
                "LibnxError_BadQueryMemory"
            },
            {
                0xF59,
                "LibnxError_AlreadyInitialized"
            },
            {
                0x1159,
                "LibnxError_NotInitialized"
            },
            {
                0x1359,
                "LibnxError_NotFound"
            },
            {
                0x1559,
                "LibnxError_IoError"
            },
            {
                0x1759,
                "LibnxError_BadInput"
            },
            {
                0x1959,
                "LibnxError_BadReent"
            },
            {
                0x1B59,
                "LibnxError_BufferProducerError"
            },
            {
                0x1D59,
                "LibnxError_HandleTooEarly"
            },
            {
                0x1F59,
                "LibnxError_HeapAllocFailed"
            },
            {
                0x2159,
                "LibnxError_TooManyOverrides"
            },
            {
                0x2359,
                "LibnxError_ParcelError"
            },
            {
                0x2559,
                "LibnxError_BadGfxInit"
            },
            {
                0x2759,
                "LibnxError_BadGfxEventWait"
            },
            {
                0x2959,
                "LibnxError_BadGfxQueueBuffer"
            },
            {
                0x2B59,
                "LibnxError_BadGfxDequeueBuffer"
            },
            {
                0x2D59,
                "LibnxError_AppletCmdidNotFound"
            },
            {
                0x2F59,
                "LibnxError_BadAppletReceiveMessage"
            },
            {
                0x3159,
                "LibnxError_BadAppletNotifyRunning"
            },
            {
                0x3359,
                "LibnxError_BadAppletGetCurrentFocusState"
            },
            {
                0x3559,
                "LibnxError_BadAppletGetOperationMode"
            },
            {
                0x3759,
                "LibnxError_BadAppletGetPerformanceMode"
            },
            {
                0x3959,
                "LibnxError_BadUsbCommsRead"
            },
            {
                0x3B59,
                "LibnxError_BadUsbCommsWrite"
            },
            {
                0x3D59,
                "LibnxError_InitFail_SM"
            },
            {
                0x3F59,
                "LibnxError_InitFail_AM"
            },
            {
                0x4159,
                "LibnxError_InitFail_HID"
            },
            {
                0x4359,
                "LibnxError_InitFail_FS"
            },
            {
                0x4559,
                "LibnxError_BadGetInfo_Rng"
            },
            {
                0x4759,
                "LibnxError_JitUnavailable"
            },
            {
                0x4959,
                "LibnxError_WeirdKernel"
            },
            {
                0x4B59,
                "LibnxError_IncompatSysVer"
            },
            {
                0x4D59,
                "LibnxError_InitFail_Time"
            },
            {
                0x4F59,
                "LibnxError_TooManyDevOpTabs"
            },
            {
                0x5159,
                "LibnxError_DomainMessageUnknownType"
            },
            {
                0x5359,
                "LibnxError_DomainMessageTooManyObjectIds"
            },
            {
                0x5559,
                "LibnxError_AppletFailedToInitialize"
            },
            {
                0x5759,
                "LibnxError_ApmFailedToInitialize"
            },
            {
                0x5959,
                "LibnxError_NvinfoFailedToInitialize"
            },
            {
                0x5B59,
                "LibnxError_NvbufFailedToInitialize"
            },
            {
                0x5D59,
                "LibnxError_LibAppletBadExit"
            },
            {
                0x35D,
                "LibnxBinderError_Unknown"
            },
            {
                0x55D,
                "LibnxBinderError_NoMemory"
            },
            {
                0x75D,
                "LibnxBinderError_InvalidOperation"
            },
            {
                0x95D,
                "LibnxBinderError_BadValue"
            },
            {
                0xB5D,
                "LibnxBinderError_BadType"
            },
            {
                0xD5D,
                "LibnxBinderError_NameNotFound"
            },
            {
                0xF5D,
                "LibnxBinderError_PermissionDenied"
            },
            {
                0x115D,
                "LibnxBinderError_NoInit"
            },
            {
                0x135D,
                "LibnxBinderError_AlreadyExists"
            },
            {
                0x155D,
                "LibnxBinderError_DeadObject"
            },
            {
                0x175D,
                "LibnxBinderError_FailedTransaction"
            },
            {
                0x195D,
                "LibnxBinderError_BadIndex"
            },
            {
                0x1B5D,
                "LibnxBinderError_NotEnoughData"
            },
            {
                0x1D5D,
                "LibnxBinderError_WouldBlock"
            },
            {
                0x1F5D,
                "LibnxBinderError_TimedOut"
            },
            {
                0x215D,
                "LibnxBinderError_UnknownTransaction"
            },
            {
                0x235D,
                "LibnxBinderError_FdsNotAllowed"
            },
            {
                0x35C,
                "LibnxNvidiaError_Unknown"
            },
            {
                0x55C,
                "LibnxNvidiaError_NotImplemented"
            },
            {
                0x75C,
                "LibnxNvidiaError_NotSupported"
            },
            {
                0x95C,
                "LibnxNvidiaError_NotInitialized"
            },
            {
                0xB5C,
                "LibnxNvidiaError_BadParameter"
            },
            {
                0xD5C,
                "LibnxNvidiaError_Timeout"
            },
            {
                0xF5C,
                "LibnxNvidiaError_InsufficientMemory"
            },
            {
                0x115C,
                "LibnxNvidiaError_ReadOnlyAttribute"
            },
            {
                0x135C,
                "LibnxNvidiaError_InvalidState"
            },
            {
                0x155C,
                "LibnxNvidiaError_InvalidAddress"
            },
            {
                0x175C,
                "LibnxNvidiaError_InvalidSize"
            },
            {
                0x195C,
                "LibnxNvidiaError_BadValue"
            },
            {
                0x1B5C,
                "LibnxNvidiaError_AlreadyAllocated"
            },
            {
                0x1D5C,
                "LibnxNvidiaError_Busy"
            },
            {
                0x1F5C,
                "LibnxNvidiaError_ResourceError"
            },
            {
                0x215C,
                "LibnxNvidiaError_CountMismatch"
            },
            {
                0x235C,
                "LibnxNvidiaError_SharedMemoryTooSmall"
            },
            {
                0x255C,
                "LibnxNvidiaError_FileOperationFailed"
            },
            {
                0x275C,
                "LibnxNvidiaError_IoctlFailed"
            },
            {
                0x7E12B,
                "Eshop connection failed"
            },
            {
                0x39D689,
                "CDN Ban"
            },
            {
                0x3E8E7C,
                "Error in account login/creation"
            },
            {
                0x3E8EA0,
                "Failed connection test"
            },
            {
                0x1F4E7C,
                "(normal) console ban"
            },
            {
                0x27EE7C,
                "(potential) complete account ban"
            },
            {
                0x36B72B,
                "Access token expired"
            },
            {
                0x1F486E,
                "Internet connection lost because the console entered sleep mode."
            },
            {
                0x21C89,
                "Failed to base64-encode the EticketDeviceCertificate during an attempted AccountGetDynamicEtickets (personalized ticket) request to ecs."
            },
            {
                0x5089,
                "Failed to snprintf the AccountGetDynamicEtickets (personalized ticket) request JSON data."
            },
            {
                0x6410,
                "GetApplicationControlData: unable to find control for the input title ID"
            },
            {
                0xA073,
                "NFC is disabled"
            },
            {
                0x16473,
                "Could not mount tag (invalid tag type?)"
            },
            {
                0x8073,
                "Device unavailable"
            },
            {
                0x10073,
                "App area not found"
            },
            {
                0x11073,
                "Tag corrupted?"
            },
            {
                0xC880,
                "thrown by AM when qlaunch is terminated"
            },
            {
                0xC87C,
                "invalid user"
            },
            {
                0xC7E,
                "mii already exists"
            },
            {
                0xA7E,
                "full database"
            },
            {
                0x115B,
                "[HBL] Stopped loading NROs"
            },
            {
                0x48C69,
                "device_cert_ecc_b223 failed to load"
            },
            {
                0x13DC02,
                "gc sector start is out of range for partition 1"
            },
            {
                0x13D802,
                "gc sector end out of range for partition 1"
            },
            {
                0x13DA02,
                "gc sector wrong partition access"
            },
            {
                0xCAFEF,
                "Atmosphere: Version Mismatch"
            },
            {
                0xA7200,
                "Fake-Error by Pegaswitch"
            },
            {
                0x0,
                "Happens in various situations, not necessarily an error, but still prevents booting.\n\nIf you got this because you downgraded, it's because you downgraded between major/key versions (7.0.x -> 6.2.0, 6.2.0 -> 6.1.0 etc) without console initialization (deleting system save files).\n\nTo recover from that: Delete system all save files except 80...120. Keep in mind that this will effectively be a factory reset."
            }
        };

        public Dictionary<int, string> switch_modules = new()
        {
            {
                1,
                "Kernel "
            },
            {
                2,
                "FS "
            },
            {
                3,
                "OS (Memory, Thread, Mutex, NVIDIA) "
            },
            {
                4,
                "HTCS "
            },
            {
                5,
                "NCM "
            },
            {
                6,
                "DD "
            },
            {
                7,
                "Debug Monitor "
            },
            {
                8,
                "LR "
            },
            {
                9,
                "Loader "
            },
            {
                10,
                "CMIF (IPC command interface) "
            },
            {
                11,
                "HIPC (IPC) "
            },
            {
                15,
                "PM "
            },
            {
                16,
                "NS "
            },
            {
                17,
                "Sockets "
            },
            {
                18,
                "HTC "
            },
            {
                20,
                "NCM Content "
            },
            {
                21,
                "SM "
            },
            {
                22,
                "RO userland "
            },
            {
                24,
                "SDMMC "
            },
            {
                25,
                "OVLN "
            },
            {
                26,
                "SPL "
            },
            {
                30,
                "Bus"
            },
            {
                100,
                "ETHC "
            },
            {
                101,
                "I2C "
            },
            {
                102,
                "GPIO "
            },
            {
                103,
                "UART "
            },
            {
                105,
                "Settings "
            },
            {
                107,
                "WLAN "
            },
            {
                108,
                "XCD "
            },
            {
                110,
                "NIFM "
            },
            {
                111,
                "Hwopus "
            },
            {
                113,
                "Bluetooth "
            },
            {
                114,
                "VI "
            },
            {
                115,
                "NFP "
            },
            {
                116,
                "Time "
            },
            {
                117,
                "FGM "
            },
            {
                118,
                "OE "
            },
            {
                120,
                "PCIe "
            },
            {
                121,
                "Friends "
            },
            {
                122,
                "BCAT "
            },
            {
                123,
                "SSL "
            },
            {
                124,
                "Account "
            },
            {
                125,
                "News "
            },
            {
                126,
                "Mii "
            },
            {
                127,
                "NFC "
            },
            {
                128,
                "AM "
            },
            {
                129,
                "Play Report "
            },
            {
                130,
                "AHID "
            },
            {
                132,
                "Home Menu (Qlaunch) "
            },
            {
                133,
                "PCV "
            },
            {
                134,
                "OMM "
            },
            {
                135,
                "BPC "
            },
            {
                136,
                "PSM "
            },
            {
                137,
                "NIM "
            },
            {
                138,
                "PSC "
            },
            {
                139,
                "TC "
            },
            {
                140,
                "USB "
            },
            {
                141,
                "NSD "
            },
            {
                142,
                "PCTL "
            },
            {
                143,
                "BTM "
            },
            {
                144,
                "LA (Library Applet)"
            },
            {
                145,
                "ETicket "
            },
            {
                146,
                "NGC (Bad Words) "
            },
            {
                147,
                "Error Report "
            },
            {
                148,
                "APM "
            },
            {
                149,
                "CEC"
            },
            {
                150,
                "Profiler "
            },
            {
                151,
                "Error Upload "
            },
            {
                153,
                "Audio "
            },
            {
                154,
                "NPNS "
            },
            {
                155,
                "NPNS HTTP Stream "
            },
            {
                157,
                "ARP "
            },
            {
                158,
                "Updater "
            },
            {
                159,
                "SWKBD "
            },
            {
                161,
                "NFC Mifare "
            },
            {
                162,
                "Userland assert "
            },
            {
                163,
                "Fatal "
            },
            {
                164,
                "NIM Shop "
            },
            {
                165,
                "SPSM "
            },
            {
                167,
                "BGTC "
            },
            {
                168,
                "Userland crash "
            },
            {
                178,
                "PDM"
            },
            {
                179,
                "OLSC "
            },
            {
                180,
                "SREPO "
            },
            {
                181,
                "Dauth "
            },
            {
                191,
                "RTC"
            },
            {
                192,
                "Regulator"
            },
            {
                197,
                "Clkrst"
            },
            {
                202,
                "HID "
            },
            {
                203,
                "LDN "
            },
            {
                205,
                "Irsensor "
            },
            {
                206,
                "Capture "
            },
            {
                208,
                "Manu "
            },
            {
                209,
                "ATK "
            },
            {
                210,
                "Web "
            },
            {
                211,
                " "
            },
            {
                212,
                "GRC "
            },
            {
                216,
                "Migration "
            },
            {
                217,
                "Migration Idc Server "
            },
            {
                345,
                "libnx "
            },
            {
                346,
                "Homebrew ABI "
            },
            {
                347,
                "Homebrew Loader "
            },
            {
                348,
                "libnx Nvidia"
            },
            {
                349,
                "libnx Binder"
            },
            {
                800,
                "General web-applet"
            },
            {
                809,
                "WifiWebAuthApplet"
            },
            {
                810,
                "Whitelisted-applet"
            },
            {
                811,
                "ShopN"
            },
            {
                311,
                "SwitchPresence"
            }
        };

        public Dictionary<string, string> switch_support_page = new()
        {
            {
                "2005-0003",
                "This error code may indicate an issue related to the microSD card being used. (https://en-americas-support.nintendo.com/app/answers/detail/a_id/22393/p/897)"
            },
            {
                "2110-1100",
                "This error code typically indicates that the Nintendo Switch console was unable to detect a network which matches any of the saved networks within the Internet settings. (https://en-americas-support.nintendo.com/app/answers/detail/a_id/22780/p/897)"
            },
            {
                "2618-0516",
                "This error code generally indicates that your network is not optimal for peer to peer connections, likely due to your network's NAT type. (https://en-americas-support.nintendo.com/app/answers/detail/a_id/25855/p/897)"
            },
            {
                "2110-2003",
                "Error codes in this range generally indicate an error occurred when the Nintendo Switch console attempted to initially connect to a wireless router (usually prior to obtaining an IP address). (https://en-americas-support.nintendo.com/app/answers/detail/a_id/27023/p/897)"
            },
            {
                "2813-6838",
                "You are unable to redeem a Nintendo eShop Card (https://en-americas-support.nintendo.com/app/answers/detail/a_id/22630/p/897)"
            },
            {
                "2813-6561",
                "In most cases, this error indicates the Nintendo eShop card or download code was entered incorrectly, or was intended for a different region's Nintendo eShop. (https://en-americas-support.nintendo.com/app/answers/detail/a_id/25870/p/897)"
            },
            {
                "2618-0513",
                "This error code generally indicates that your network is not optimal for peer to peer connections, this may be due to the ISP, Internet connection speeds, or due to your network's NAT type. (https://en-americas-support.nintendo.com/app/answers/detail/a_id/25980/p/897)"
            },
            {
                "2618-0201",
                "This error may be the result your connection timing out due to a slow Internet service or a poor wireless environment. (https://en-americas-support.nintendo.com/app/answers/detail/a_id/25866/p/897)"
            },
            {
                "2002-0001",
                "An error code is received when powering up the Nintendo Switch console, or when coming out of sleep mode. (https://en-americas-support.nintendo.com/app/answers/detail/a_id/27167/p/897)"
            },
            {
                "2813-1470",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/26362/p/897"
            },
            {
                "2124-4007",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/28046/p/897"
            },
            {
                "2811-5001",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22392/p/897"
            },
            {
                "2110-3127",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22567/p/897"
            },
            {
                "9001-0026",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/27311/p/897"
            },
            {
                "2124-8006",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/25858/p/897"
            },
            {
                "2124-8007",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/25858/p/897"
            },
            {
                "2137-8006",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22493/p/897"
            },
            {
                "2155-8007",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/42264/p/897"
            },
            {
                "2811-1006",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/25859/p/897"
            },
            {
                "2813-0055",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/27056/p/897"
            },
            {
                "2181-4008",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/42061/p/897"
            },
            {
                "2137-8056",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/28910/p/897"
            },
            {
                "2618-0502",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/25865/p/897"
            },
            {
                "2618-0501",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/25865/p/897"
            },
            {
                "2162-0002",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22596/p/897"
            },
            {
                "2137-8035",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22298/p/897"
            },
            {
                "2618-0006",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/25856/p/897"
            },
            {
                "2016-0641",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/27004/p/897"
            },
            {
                "2016-0247",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22720/p/897"
            },
            {
                "2124-8028",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22443/p/897"
            },
            {
                "2811-1028",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22503/p/897"
            },
            {
                "2306-0303",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/42878/p/897"
            },
            {
                "2123-0301",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/28291/p/897"
            },
            {
                "2168-0002",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22518/p/897"
            },
            {
                "2160-8007",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/28530/p/897"
            },
            {
                "2160-8006",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/28530/p/897"
            },
            {
                "2101-0001",
                "https://en-americas-support.nintendo.com/app/answers/detail/a_id/22624/p/897"
            }
        };
    }
}