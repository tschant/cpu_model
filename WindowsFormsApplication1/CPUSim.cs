/*
**
 *  Tarryn Schantell
 *  2595127
 *  EECS 643 Final Project- MIPS CPU Simulation
 * 
 *  Runs a simulation of a MIPS CPU while using pipeline registers, forwarding,
 *        and predicting branch not taken.
 *  Loads an input file that has the registers, memory, and code that will be used 
 *      for the simulation. Register and memory are not required for succesful execution.
 *      Also loads an output file, where cycle information will be written to, along with the GUI
 *  
 * There is potential for infinite loop if branch is always taken, and is at the beginning of "code" section
 *      of the input file (if variable it checks is never eqaul to 0). 
**
*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApplication1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
		//input file
        private void inputButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            string filename = "";
            string path = "";
            if (ofd.ShowDialog() == DialogResult.OK) // Test result.
            {
                filename = System.IO.Path.GetFileName(ofd.FileName);
                path = System.IO.Path.GetDirectoryName(ofd.FileName);
            }
            input.Text = path + "\\" + filename;
        }
		//output file
        private void outputButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            string filename = "";
            string path = "";
            if (ofd.ShowDialog() == DialogResult.OK) // Test result.
            {
                filename = System.IO.Path.GetFileName(ofd.FileName);
                path = System.IO.Path.GetDirectoryName(ofd.FileName);
            }
            output.Text = path + "\\" + filename;
        }
		//start the simulation
        private void start_Click(object sender, EventArgs e)
        {
		//resets the cycle, instruction count, and where the code starts
            cycle_count = 0;
            instr_count = 0;
            codeStart = 0;
            if (input.Text == "")
            {
                //default case for input file
                input.Text = ".//input.txt";
            }
            if (output.Text == "")
            {
                //default case for output file
                output.Text = ".//output.txt";
            }
			//load input and output into the simulation
            startSim(input.Text, output.Text);
        }
		//used to write to text box of GUI
        public void WriteText(String message)
        {
            textBox1.AppendText(message + "   ");
        }
        private void close_Click(object sender, EventArgs e)
        {
            Close();
        }
		//input and output file
        string inputFile;
        string outputFile;
		//cycle and instruction count
        int instr_count = 0;
        int cycle_count;
        int codeStart;

        public void startSim(string input, string output)
        {
			//determines which section of the input is being parsed
            char state = 'o'; //had to be defined to some random value
            inputFile = input;
            outputFile = output;
            //deletes the old output file
            System.IO.File.Delete(outputFile);
            //read the input file into a string array, each line is new entry in string
            string[] line = System.IO.File.ReadAllLines(inputFile);

            //step through the input string line by line
            for (int i = 0; i < line.Length; i++)
            {
                //textBox1.AppendText("\r\n" + line[i]); //debugging
                //determine what the next segment of the input file will do
                switch (line[i])
                {
                    case "REGISTERS":
                    case "REGISTER":
                        state = 'r';
                        continue;
                    case "MEMORY":
                        state = 'm';
                        continue;
                    case "CODE":
                        state = 'c';
                        codeStart = i;
                        continue;
                    default:
                        break;
                }
				//uses the predetermined state for the next entries of the array
                switch (state)
                {
                    //parse the memory
                    case 'm':
                        memory_add(line[i]);
                        break;
                    //parse registers
                    case 'r':
                        register(line[i]);
                        break;
                    //parse the code
                    case 'c':
						//code section has its own function
						//the rest of the string array is placed into a new array
                        int length = line.Length - i;
                        string[] rest = new string[length];
                        int n = 0;
                        for (; i < line.Length; i++)
                        {
                            rest[n] = line[i];
                            n++;
                        }
						//and used with the code execution function
                        codeEx(rest);
                        break;
                    default:
                        break;
                }
                
            }
			//confirm completion of simulation in the GUI
            textBox1.AppendText("\r\n\r\nEXECUTION FINISHED");
			//empties the memory for next execution
            memory.Clear();
            memoryLocation.Clear();
        }
		
		//locates and stores the location of branch address before execution
        string branch;
        int branchAddr;
		//this is executed at the beginning of code execution, but not loaded for use until ID stage
        public void branchLocation(string[] code)
        {
            //finds location of branch instruction
            for (int i = 0; i < code.Length; i++)
            {
                char[] delimiterChars = { ' ', ',', '\t' };
                //splits the input string into subparts, and removes empty entries
                string[] temp = code[i].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                if (temp.Length >= 3)
                {
                    if (temp[0].Equals("BNEZ"))
                    {   
                        //determines the label that indicates the jump location
                        branch = temp[2];
                        break;
                    }
                }
            }
            //finds where the branch jumps to if taken
            for (int i = 0; i < code.Length; i++)
            {
                char[] delimiterChars = { ' ', ',', '\t' };
                //splits the input string into subparts, and removes empty entries
                string[] temp = code[i].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                if (temp.Length >= 3)
                {
                    if (temp[0].Equals(branch + ":"))
                    {
                        branchAddr = i;
                        break;
                    }
                }
            }
        }

        public void codeEx(string[] code)
        {
            int i = 0;
            string codeStr;
            branchLocation(code); //value not used until ID/EX stage of branch instr
            do
            {
                //does the execution of each line
                cycle_count++;
				//writes the current cycle to the GUI and to the output file
                WriteText("\r\n c#" + cycle_count);
                System.IO.File.AppendAllText(outputFile, Environment.NewLine + "c#" + cycle_count + " ");
                wb(); //write the output of the Write back function
                mem3();
                mem2();
                mem1();
                ex();
                id();
                if2();
                //fetches the next instruction and sends to IF1
                if (i < code.Length && !hazardCheck() && !branchChk && code[i] != "")
                {
                    codeStr = code[i];
                }
                //if there is a hazard, next instruction null, and go back a line
                else if (hazardCheck())
                {
                    codeStr = null;
                    i--;
                }
                //if branch is taken, reset branch val, next instr is target inst, and i = address
                else if (branchChk)
                {
                    branchChk = false;
                    codeStr = code[branchAddr];
                    i = branchAddr;
                    IdEx = null;
                    If2Id = null;
                    If1If2 = null;
                    continue;
                }
                //once all lines have been executed, continue with null instr until finished
                else
                {
                    //replaces the end with null
                    codeStr = null;
                }
                if1(codeStr, i);
                i++;
            } while (checkPipeline());
			
            //output to file and textbox of GUI
            textBox1.AppendText("\r\nREGISTERS");
            System.IO.File.AppendAllText(outputFile, Environment.NewLine + "REGISTERS");
            for (i = 0; i < registers.Length; i++)
            {
                if (registers[i] != 0)
                {
                    textBox1.AppendText("\r\nR" + i + " " + registers[i]);
                    System.IO.File.AppendAllText(outputFile, Environment.NewLine + "R" + i + " " + registers[i]);
                }
            }
            textBox1.AppendText("\r\nMEMORY");
            System.IO.File.AppendAllText(outputFile, Environment.NewLine + "MEMORY");
            for (i = 0; i < memory.Count; i++)
            {
                if (memory[i] != 0)
                {
                    textBox1.AppendText("\r\n" + memoryLocation[i] + " " + memory[i]);
                    System.IO.File.AppendAllText(outputFile, Environment.NewLine + i + " " + memory[i]);
                }
            }

        }
        public bool checkPipeline()
        {
            //makes sure that all instructions have completed by checking if all registers are null
            bool chk = true;
            if (If1If2 == null && If2Id == null && IdEx == null && ExM1 == null && M1M2 == null && M2M3 == null && M3Wb == null)
                chk = false;
            return chk;
        }
        int[] registers = new int[32];
		//list is used for dynamic size of memory
        List<int> memory = new List<int>(); 
        List<int> memoryLocation = new List<int>();
        //pipeline registers
        //after IF1 the stages are not pased any values, they all use the pipline registers
        string[] If1If2;
        string[] If2Id;
        string[] IdEx;
        string[] ExM1;
        string[] M1M2;
        string[] M2M3;
        string[] M3Wb;
        //pipeline reg = [inst, rd, rs, rt]
        bool branchChk = false; //if branch is taken or not
		//populates the memory
        public void memory_add(string mem)
        {
            if (mem != "")
            {
                char[] delimiterChars = { ' ', ',', '\t' };
                string[] mems = mem.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                //if (Convert.ToInt32(mems[0]) < memory.Length)
                memoryLocation.Add(Convert.ToInt32(mems[0]));
                memory.Add(Convert.ToInt32(mems[1])); //= Convert.ToInt32(mems[1]);
            }
        }
		//populates the register values
        public void register(string reg)
        {
            if (reg != "")
            {
                int i = 0;
                char[] delimiterChars = { ' ', ',', '\t', 'R' };
                string[] regs = reg.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                i = Convert.ToInt32(regs[0]);
                registers[i] = Convert.ToInt32(regs[1]);
            }
        }
		//***********************************************pipeline code*****************************************************
        public string wb()
        {
			//since forwarding happens (value is written is execution) 
			//nothing is written here so the values do not overwrite each
            string output = "";
            if (M3Wb != null)
            {
                int inst = M3Wb.Length - 1;
                textBox1.AppendText(M3Wb[inst] + "- WB ");
                System.IO.File.AppendAllText(outputFile, M3Wb[inst] + "- WB ");
                M3Wb = null; //clears the current pipeline register after a succesful execution
            }
            return output; 
        }
        bool chk;
        public void mem3()
        {
            //tag check
            if (M2M3 != null)
            {
                M3Wb = M2M3;
                //check tag
                switch (M2M3[0])
                {
                    case "LD":
                        //value does not exist, so load a 0
                        if (!chk)
                        {
                            registers[Convert.ToInt32(M2M3[1])] = 0;
                        }
                        break;
                    case "SD":
                        if (!chk)
                        {
                            memoryLocation.Add(Convert.ToInt32(M2M3[2]));
                            memory.Add(Convert.ToInt32(M2M3[1]));
                        }
                        break;
                    default:
                        break;
                }
                int inst = M3Wb.Length - 1;
                textBox1.AppendText(M3Wb[inst] +  "- M3 ");
                System.IO.File.AppendAllText(outputFile, M3Wb[inst] + "- M3 ");
                M2M3 = null;
            }
            else
                M3Wb = null;
        }
        public void mem2()
        {
            if (M1M2 != null)
            {
                M2M3 = M1M2;
                //used to check if value exisits in memory
                chk = false;
                switch (M1M2[0])
                {
                    case "LD":
                        //registers[Convert.ToInt32(M1M2[1])] = memory[Convert.ToInt32(M1M2[2])];
                        for (int i = 0; i < memoryLocation.Count; i++)
                        {
                            if(Convert.ToInt32(M1M2[2]) == memoryLocation[i])
                            {
                                registers[Convert.ToInt32(M1M2[1])] = memory[i];
                                //value does exist in memory
                                chk = true;
                            }
                        }

                        break;
                    case "SD":
                        for (int i = 0; i < memoryLocation.Count; i++)
                        {
                                if (Convert.ToInt32(M1M2[2]) == memoryLocation[i])
                                {
                                    memory[i] = Convert.ToInt32(M1M2[1]);
                                    chk = true;
                                }
                        }
                        //memory[registers[Convert.ToInt32(M1M2[2])]] = Convert.ToInt32(M1M2[1]);
                        break;
                    default:
                        break;
                }
                int inst = M2M3.Length - 1;
                textBox1.AppendText(M2M3[inst] +  "- M2 ");
                System.IO.File.AppendAllText(outputFile, M2M3[inst] + "- M2 ");
                M1M2 = null;
            }
            else
                M2M3 = null;
        }
        public void mem1()
        {
            if (ExM1 != null)
            {
                M1M2 = ExM1;
                switch (ExM1[0])
                {
                    case "LD":
                        //M1M2[2] = Convert.ToString(registers[(Convert.ToInt32(ExM1[2]))]);
                        break;
                    case "SD":
                        M1M2[1] = Convert.ToString(registers[(Convert.ToInt32(ExM1[1]))]);
                        break;
                    default:
                        break;
                }
                int inst = M1M2.Length - 1;
                textBox1.AppendText(M1M2[inst] + "- M1 ");
                System.IO.File.AppendAllText(outputFile, M1M2[inst] + "- M1 ");
                ExM1 = null;
            }
            else
                M1M2 = null;
        }
        //used to determine if a constant is present
        public int regConstParse(string[] R)
        {
            int r;
            int n = R[0].IndexOf("#");
            //if '#' split the string
            if (n != -1)
            {
                char[] splitTemp = { '#' };
                R = R[0].Split(splitTemp, StringSplitOptions.RemoveEmptyEntries);
                r = Convert.ToInt32(R[0]);
            }
            else
            {
                //if constant is not present, value is in registers
                r = registers[Convert.ToInt32(R[0])];
            }
            return r;
        }
        public void ex()
        {
            if (IdEx != null && !hazardCheck())
            {
                ExM1 = IdEx;
                //used to parse the string to the correct registers
                char[] delimiterChars = { '(', ')', 'R'};
                //register location from string to int conversion
                string[] RD;
                int rd;
                string[] RS;
                int rs;
                string[] RT;
                int rt;
                switch (IdEx[0])
                {
                    case "LD":
					//address calculation
                        RD = IdEx[1].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        RS = IdEx[2].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        ExM1[1] = RD[0];
                        ExM1[2] = Convert.ToString(Convert.ToInt32(RS[0]) + registers[Convert.ToInt32(RS[1])]);
                        break;
                    case "DADD":
                        RD = IdEx[1].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        RS = IdEx[2].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        RT = IdEx[3].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        //System.Console.WriteLine("RD= " + RD[0] + " RS= " + RS[0] + " RT= "+ RT[0]);
                        rd = Convert.ToInt32(RD[0]);
                        rs = regConstParse(RS);
                        rt = regConstParse(RT);
                        //writes the value to the destination (psuedo-forwarding)
                        registers[rd] = rs + rt;
                        break;
                    case "SUB":
                        RD = IdEx[1].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        RS = IdEx[2].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        RT = IdEx[3].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        //System.Console.WriteLine("RD= " + RD[0] + " RS= " + RS[0] + " RT= "+ RT[0]);
                        rd = Convert.ToInt32(RD[0]);
                        rs = regConstParse(RS);
                        rt = regConstParse(RT);
                        //writes the value to the destination (psuedo-forwarding)
                        registers[rd] = rs - rt;
                        break;
                    case "BNEZ":
                        RD = IdEx[1].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        //System.Console.WriteLine("RD= " + RD[0]);
                        rd = Convert.ToInt32(RD[0]);
                        if (registers[rd] != 0)
                        {
                            System.Console.WriteLine("Next Instr At: " + branchAddr + " " + registers[rd]);
                            branchChk = true;
                        }
                        else
                        {
                            System.Console.WriteLine("Branch Not Taken");
                        }
                        break;
                    case "SD":
                        RD = IdEx[1].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        RS = IdEx[2].Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                        ExM1[1] = RD[0];
                        ExM1[2] = Convert.ToString(Convert.ToInt32(RS[0]) + registers[Convert.ToInt32(RS[1])]);
                        break;
                    default:
                        break;
                }

                int inst = ExM1.Length - 1;
                textBox1.AppendText(ExM1[inst] +  "- Ex ");
                System.IO.File.AppendAllText(outputFile, ExM1[inst] + "- Ex ");
                IdEx = null;
            }
            else
                ExM1 = null;
        }
        public bool hazardCheck()
        {
            bool hazard = false;
			//check the current executed instruction
            if (If2Id != null && ExM1 != null && ExM1.Length > 1 && If2Id.Length > 3)
            {
                if ("R" + ExM1[1] == If2Id[2] || "R" + ExM1[1] == If2Id[3])
                {
                    hazard = true;
                }
            }
			//memory 1 instruction
            if(IdEx != null && M1M2 != null && M1M2.Length > 1 && IdEx.Length > 3)
            {
                if ("R" + M1M2[1] == IdEx[2] || "R" + M1M2[1] == IdEx[3])
                {
                    hazard = true;
                }
            }
			//memory 2 instruction
            if (IdEx != null && M2M3 != null && M2M3.Length > 1 && IdEx.Length > 3)
            {
                if ("R" + M2M3[1] == IdEx[2] || "R" + M2M3[1] == IdEx[3])
                {
                    hazard = true;
                }
            }
            return hazard;
        }
        //decode the instruction
        public void id()
        {
            if (If2Id != null)
            {	
				//check hazard and if next instruction is not empty (meaning the previous instruction finished execution)
                if (hazardCheck() && IdEx != null)
                {
                    int inst = IdEx.Length - 1;
                    textBox1.AppendText(IdEx[inst] + "- stall ");
                    System.IO.File.AppendAllText(outputFile, IdEx[inst] + "- stall ");
                }
                else
                {
                    IdEx = If2Id;
                    //decode instruction
                    switch(If2Id[0])
                    {
                        case "LD":
                            System.Console.WriteLine("Load Instr");
                            break;
                        case "DADD":
                            System.Console.WriteLine("Add Instr");
                            break;
                        case "BNEZ":
                            //loads the branch location for use
                            System.Console.WriteLine("Branch Instr to line " + branchAddr.ToString());
                            break;
                        case "SD":
                            System.Console.WriteLine("Store Instr");
                            break;
                        default:
                            break;
                    }
                    int inst = IdEx.Length - 1;
                    textBox1.AppendText(IdEx[inst] + "- ID ");
                    System.IO.File.AppendAllText(outputFile, IdEx[inst] + "- ID ");
                    If2Id = null;
                }
            }
            else
                IdEx = null;
        }
        public void if2()
        {
            if (hazardCheck() && If2Id != null)
            {
                int inst = If2Id.Length - 1;
                textBox1.AppendText(If2Id[inst] + "- stall ");
                System.IO.File.AppendAllText(outputFile, If2Id[inst] + "- stall ");
            }
            else 
            if (If1If2 != null)
            {
                If2Id = If1If2;
                //for (int i = 0; i < If2Id.Length; i++)
                    //System.Console.WriteLine(If2Id[i]);
                int inst = If2Id.Length - 1;
                textBox1.AppendText(If2Id[inst] + "- IF2 ");
                System.IO.File.AppendAllText(outputFile, If2Id[inst] + "- IF2 ");
                If1If2 = null;
            }
            else
                If2Id = null;
        }
        //get the current instruction
        public void if1(string input, int lineNumber)
        {
            if (hazardCheck() && If1If2 != null)
            {
                int inst = If1If2.Length - 1;
                textBox1.AppendText(If1If2[inst] + "- stall ");
                System.IO.File.AppendAllText(outputFile, If1If2[inst] + "- stall ");
            }
            else 
            if (input != null)
            {
                instr_count +=  1;
                char[] delimiterChars = { ' ', ',', '\t' };
                input = string.Concat(input, ", I", instr_count.ToString());
                //splits the input string into subparts, and removes empty entries
                If1If2 = input.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                //remove the branch label
                if(If1If2[0].Equals(branch + ":"))
                    for (int i = 0; i < If1If2.Length - 1; i++)
                    {
                        If1If2[i] = If1If2[i + 1];
                    }
                int inst = If1If2.Length - 1;
                textBox1.AppendText(If1If2[inst] + "- IF1 ");
                System.IO.File.AppendAllText(outputFile, If1If2[inst] + "- IF1 ");
            }
            else
                If1If2 = null;
        }
		//***********************************************pipeline code*****************************************************
    }
}